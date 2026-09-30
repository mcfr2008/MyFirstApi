-- Feature: Partition TrackingEvents by month (performance / data lifecycle)
--
-- TrackingEvents is by far the fastest-growing table (one row per item per scan
-- or shipment movement). Range-partitioning it by "OccurredAt" month keeps
-- indexes small, lets date-range queries skip whole months, and lets old months
-- be detached/archived in one step instead of deleting millions of rows.
--
-- Notes:
--   * The primary key must include the partition key: ("Id", "OccurredAt").
--     "Id" stays globally unique because it still comes from one sequence.
--   * "ReplacesEventId" can't have a foreign key to a partitioned table's
--     partial key, so the application enforces it (TrackingEventService.CorrectAsync).
--   * Partitions are created ahead of time by create_tracking_event_partitions(),
--     called daily by the API (Services/PartitionMaintenanceService.cs) and
--     manually via Scripts/maintenance/create_partitions.sql.
--   * Rows outside every monthly partition (e.g. back-dated events older than the
--     first partition) land in "TrackingEvents_default"; health_check.sql reports it.

-- Creates the partition for one calendar month (UTC). Returns false if it exists.
CREATE OR REPLACE FUNCTION create_tracking_event_partition(month_start DATE)
RETURNS BOOLEAN
LANGUAGE plpgsql AS $$
DECLARE
    partition_name TEXT := format('TrackingEvents_%s', to_char(month_start, 'YYYY_MM'));
    range_from TIMESTAMPTZ := date_trunc('month', month_start)::timestamp AT TIME ZONE 'UTC';
    range_to TIMESTAMPTZ := (date_trunc('month', month_start) + INTERVAL '1 month')::timestamp AT TIME ZONE 'UTC';
BEGIN
    IF to_regclass(format('public.%I', partition_name)) IS NOT NULL THEN
        RETURN FALSE;
    END IF;

    EXECUTE format('CREATE TABLE %I PARTITION OF "TrackingEvents" FOR VALUES FROM (%L) TO (%L)',
                   partition_name, range_from, range_to);
    -- Insert-heavy: vacuum/analyze after 5% / 2% new rows instead of the 20% / 10% defaults.
    EXECUTE format('ALTER TABLE %I SET (autovacuum_vacuum_insert_scale_factor = 0.05, autovacuum_analyze_scale_factor = 0.02)',
                   partition_name);
    RETURN TRUE;
END $$;

-- Makes sure partitions exist for the current month and the next months_ahead months.
-- Returns how many were created. Safe to run any number of times.
CREATE OR REPLACE FUNCTION create_tracking_event_partitions(months_ahead INT DEFAULT 3)
RETURNS INT
LANGUAGE plpgsql AS $$
DECLARE
    created INT := 0;
    offset_months INT;
BEGIN
    FOR offset_months IN 0..months_ahead LOOP
        IF create_tracking_event_partition(
               (date_trunc('month', now() AT TIME ZONE 'UTC') + make_interval(months => offset_months))::date) THEN
            created := created + 1;
        END IF;
    END LOOP;
    RETURN created;
END $$;

-- One-time conversion of the existing plain table (skipped once partitioned).
DO $$
DECLARE
    month_start DATE;
BEGIN
    IF (SELECT relkind FROM pg_class
        WHERE relname = 'TrackingEvents' AND relnamespace = 'public'::regnamespace) <> 'r' THEN
        RETURN;
    END IF;

    -- Keep the Id sequence alive when the old table is dropped.
    ALTER SEQUENCE "TrackingEvents_Id_seq" OWNED BY NONE;
    ALTER TABLE "TrackingEvents" RENAME TO "TrackingEvents_old";
    ALTER TABLE "TrackingEvents_old" RENAME CONSTRAINT "TrackingEvents_pkey" TO "TrackingEvents_old_pkey";
    DROP INDEX IF EXISTS "IX_TrackingEvents_TrackedItemId_OccurredAt";
    DROP INDEX IF EXISTS "IX_TrackingEvents_ShipmentId";
    DROP INDEX IF EXISTS "IX_TrackingEvents_LocationId_OccurredAt";
    DROP INDEX IF EXISTS "IX_TrackingEvents_ReasonCodeId";
    DROP INDEX IF EXISTS "IX_TrackingEvents_ReplacesEventId";

    CREATE TABLE "TrackingEvents" (
        "Id" BIGINT NOT NULL DEFAULT nextval('"TrackingEvents_Id_seq"'),
        "TrackedItemId" INT NOT NULL REFERENCES "TrackedItems"("Id"),
        "EventTypeId" INT NOT NULL REFERENCES "EventTypes"("Id"),
        "LocationId" INT REFERENCES "Locations"("Id"),
        "OccurredAt" TIMESTAMPTZ NOT NULL,
        "RecordedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        "RecordedBy" VARCHAR(255),
        "Source" VARCHAR(20) NOT NULL,
        "Note" VARCHAR(2000),
        "Latitude" NUMERIC(9, 6),
        "Longitude" NUMERIC(9, 6),
        "ShipmentId" INT REFERENCES "Shipments"("Id"),
        "ShipmentLegId" INT REFERENCES "ShipmentLegs"("Id"),
        "ContainerId" INT REFERENCES "Containers"("Id"),
        "ReasonCodeId" INT REFERENCES "ReasonCodes"("Id"),
        "IsVoided" BOOLEAN NOT NULL DEFAULT FALSE,
        "VoidedAt" TIMESTAMPTZ,
        "VoidedBy" VARCHAR(255),
        "VoidReason" VARCHAR(500),
        "ReplacesEventId" BIGINT,
        "IsSystemManaged" BOOLEAN NOT NULL DEFAULT FALSE,
        PRIMARY KEY ("Id", "OccurredAt")
    ) PARTITION BY RANGE ("OccurredAt");

    FOR month_start IN
        SELECT DISTINCT date_trunc('month', "OccurredAt" AT TIME ZONE 'UTC')::date FROM "TrackingEvents_old"
    LOOP
        PERFORM create_tracking_event_partition(month_start);
    END LOOP;
    PERFORM create_tracking_event_partitions(3);
    CREATE TABLE "TrackingEvents_default" PARTITION OF "TrackingEvents" DEFAULT;

    INSERT INTO "TrackingEvents" (
        "Id", "TrackedItemId", "EventTypeId", "LocationId", "OccurredAt", "RecordedAt", "RecordedBy",
        "Source", "Note", "Latitude", "Longitude", "ShipmentId", "ShipmentLegId", "ContainerId",
        "ReasonCodeId", "IsVoided", "VoidedAt", "VoidedBy", "VoidReason", "ReplacesEventId", "IsSystemManaged")
    SELECT
        "Id", "TrackedItemId", "EventTypeId", "LocationId", "OccurredAt", "RecordedAt", "RecordedBy",
        "Source", "Note", "Latitude", "Longitude", "ShipmentId", "ShipmentLegId", "ContainerId",
        "ReasonCodeId", "IsVoided", "VoidedAt", "VoidedBy", "VoidReason", "ReplacesEventId", "IsSystemManaged"
    FROM "TrackingEvents_old";

    DROP TABLE "TrackingEvents_old";
    ALTER SEQUENCE "TrackingEvents_Id_seq" OWNED BY "TrackingEvents"."Id";
END $$;

-- Indexes on the parent are created on every partition, including future ones.
-- Item timeline / state recalculation.
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_TrackedItemId_OccurredAt" ON "TrackingEvents" ("TrackedItemId", "OccurredAt");
-- "Latest events" pages: newest-first keyset pagination merges partitions in index order.
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_OccurredAt_Id" ON "TrackingEvents" ("OccurredAt", "Id");
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_LocationId_OccurredAt" ON "TrackingEvents" ("LocationId", "OccurredAt");
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_EventTypeId_OccurredAt" ON "TrackingEvents" ("EventTypeId", "OccurredAt");
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ShipmentId" ON "TrackingEvents" ("ShipmentId") WHERE "ShipmentId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ContainerId" ON "TrackingEvents" ("ContainerId") WHERE "ContainerId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ReasonCodeId" ON "TrackingEvents" ("ReasonCodeId") WHERE "ReasonCodeId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ReplacesEventId" ON "TrackingEvents" ("ReplacesEventId") WHERE "ReplacesEventId" IS NOT NULL;

ANALYZE "TrackingEvents";
