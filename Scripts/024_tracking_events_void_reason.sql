-- Feature: Tracking events - reason codes, void / correction.

ALTER TABLE "TrackingEvents" ADD COLUMN IF NOT EXISTS "ReasonCodeId" INT REFERENCES "ReasonCodes"("Id");
ALTER TABLE "TrackingEvents" ADD COLUMN IF NOT EXISTS "IsVoided" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "TrackingEvents" ADD COLUMN IF NOT EXISTS "VoidedAt" TIMESTAMPTZ;
ALTER TABLE "TrackingEvents" ADD COLUMN IF NOT EXISTS "VoidedBy" VARCHAR(255);
ALTER TABLE "TrackingEvents" ADD COLUMN IF NOT EXISTS "VoidReason" VARCHAR(500);
ALTER TABLE "TrackingEvents" ADD COLUMN IF NOT EXISTS "ReplacesEventId" BIGINT REFERENCES "TrackingEvents"("Id");

-- Events mirroring shipment/container operations can only be undone there.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'TrackingEvents' AND column_name = 'IsSystemManaged'
    ) THEN
        ALTER TABLE "TrackingEvents" ADD COLUMN "IsSystemManaged" BOOLEAN NOT NULL DEFAULT FALSE;
        UPDATE "TrackingEvents" e SET "IsSystemManaged" = TRUE
        FROM "EventTypes" t
        WHERE t."Id" = e."EventTypeId" AND (
            e."ShipmentLegId" IS NOT NULL
            OR (e."ShipmentId" IS NOT NULL AND t."Code" IN ('DELIVERED', 'CUSTOMS_HOLD', 'CUSTOMS_CLEARED'))
            OR t."Code" IN ('LOADED_INTO_CONTAINER', 'UNLOADED_FROM_CONTAINER'));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ReasonCodeId" ON "TrackingEvents" ("ReasonCodeId");
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ReplacesEventId" ON "TrackingEvents" ("ReplacesEventId");
