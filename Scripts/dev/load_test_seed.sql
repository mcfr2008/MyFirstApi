-- DEV ONLY - not part of run_all.sql.
-- Generates a large synthetic data set to measure query performance:
--   50 locations, 200,000 tracked items, 3,000,000 tracking events spread over
--   ~18 months, 50,000 delivered shipments (1 sea leg each).
-- Everything is tagged with a LOAD prefix; remove it with load_test_cleanup.sql.
-- Usage: docker exec -i postgres-server psql -U postgres -d myfirstapi_db < Scripts/dev/load_test_seed.sql

\timing on

INSERT INTO "Locations" ("Code", "Name", "Type", "Country")
SELECT 'LOAD-LOC-' || g, 'Load test hub ' || g, 'Hub', 'TH'
FROM generate_series(1, 50) g
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "Parties" ("Code", "Name", "Type")
VALUES ('LOAD-SENDER', 'Load test sender', 'Company'), ('LOAD-RECEIVER', 'Load test receiver', 'Company')
ON CONFLICT ("Code") DO NOTHING;

-- Item g gets 15 events, 6 hours apart, starting (540 - g % 525) days ago.
-- Its current state (location, LastEventAt) matches its last generated event.
WITH locs AS (SELECT array_agg("Id" ORDER BY "Id") a FROM "Locations" WHERE "Code" LIKE 'LOAD-LOC-%')
INSERT INTO "TrackedItems" ("TagCode", "Name", "Status", "CurrentLocationId", "Attributes",
                            "LastEventAt", "CreatedAt", "UpdatedAt", "CreatedBy")
SELECT 'LOAD-' || lpad(g::text, 7, '0'),
       'Load test item ' || g,
       'InTransit',
       locs.a[1 + (g + 15) % 50],
       '{}',
       now() - interval '540 days' + (g % 525) * interval '1 day' + 15 * interval '6 hours',
       now(), now(), 'loadtest'
FROM generate_series(1, 200000) g, locs
ON CONFLICT ("TagCode") DO NOTHING;

WITH locs AS (SELECT array_agg("Id" ORDER BY "Id") a FROM "Locations" WHERE "Code" LIKE 'LOAD-LOC-%'),
     types AS (SELECT array_agg("Id" ORDER BY "Id") a FROM "EventTypes"
               WHERE NOT "RequiresReason" AND "Code" NOT IN ('DELIVERED', 'LOADED_INTO_CONTAINER', 'UNLOADED_FROM_CONTAINER')),
     items AS (SELECT "Id", substring("TagCode" from 6)::int AS g FROM "TrackedItems" WHERE "TagCode" LIKE 'LOAD-%')
INSERT INTO "TrackingEvents" ("TrackedItemId", "EventTypeId", "LocationId", "OccurredAt", "RecordedAt",
                              "RecordedBy", "Source", "IsSystemManaged", "IsVoided")
SELECT i."Id",
       types.a[1 + (i.g * 7 + n) % array_length(types.a, 1)],
       locs.a[1 + (i.g + n) % 50],
       now() - interval '540 days' + (i.g % 525) * interval '1 day' + n * interval '6 hours',
       now(), 'loadtest', 'Scan', FALSE, FALSE
FROM items i, generate_series(1, 15) n, locs, types;

WITH locs AS (SELECT array_agg("Id" ORDER BY "Id") a FROM "Locations" WHERE "Code" LIKE 'LOAD-LOC-%'),
     parties AS (SELECT min("Id") FILTER (WHERE "Code" = 'LOAD-SENDER') s,
                        min("Id") FILTER (WHERE "Code" = 'LOAD-RECEIVER') r FROM "Parties")
INSERT INTO "Shipments" ("TrackingNumber", "Reference", "SenderPartyId", "ReceiverPartyId",
                         "OriginLocationId", "DestinationLocationId", "Status", "CustomsStatus",
                         "DeliveredAt", "CreatedAt", "UpdatedAt", "CreatedBy")
SELECT 'LOADTS' || lpad(g::text, 8, '0'), 'LOAD-PO-' || g, parties.s, parties.r,
       locs.a[1 + g % 50], locs.a[1 + (g + 1) % 50], 'Delivered', 'NotRequired',
       now() - (g % 500) * interval '1 day', now(), now(), 'loadtest'
FROM generate_series(1, 50000) g, locs, parties
ON CONFLICT ("TrackingNumber") DO NOTHING;

INSERT INTO "ShipmentLegs" ("ShipmentId", "Sequence", "Mode", "OriginLocationId", "DestinationLocationId",
                            "VoyageNumber", "DocumentType", "DocumentNumber", "ActualDeparture", "ActualArrival")
SELECT s."Id", 1, 'Sea', s."OriginLocationId", s."DestinationLocationId",
       'V' || (s."Id" % 900), 'BillOfLading', 'LOADBL' || substring(s."TrackingNumber" from 7),
       s."DeliveredAt" - interval '10 days', s."DeliveredAt" - interval '1 day'
FROM "Shipments" s
WHERE s."TrackingNumber" LIKE 'LOADTS%'
ON CONFLICT ("ShipmentId", "Sequence") DO NOTHING;

ANALYZE;

SELECT (SELECT count(*) FROM "TrackedItems") AS items,
       (SELECT count(*) FROM "TrackingEvents") AS events,
       (SELECT count(*) FROM "Shipments") AS shipments;
