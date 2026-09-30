-- DEV ONLY - removes everything created by load_test_seed.sql.

\timing on

DELETE FROM "TrackingEvents" WHERE "TrackedItemId" IN (SELECT "Id" FROM "TrackedItems" WHERE "TagCode" LIKE 'LOAD-%');
DELETE FROM "ShipmentItems" WHERE "TrackedItemId" IN (SELECT "Id" FROM "TrackedItems" WHERE "TagCode" LIKE 'LOAD-%');
DELETE FROM "TrackedItems" WHERE "TagCode" LIKE 'LOAD-%';
DELETE FROM "Shipments" WHERE "TrackingNumber" LIKE 'LOADTS%';   -- legs cascade
DELETE FROM "Parties" WHERE "Code" IN ('LOAD-SENDER', 'LOAD-RECEIVER');
DELETE FROM "Locations" WHERE "Code" LIKE 'LOAD-LOC-%';

VACUUM ANALYZE;
