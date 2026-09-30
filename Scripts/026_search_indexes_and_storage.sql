-- Feature: Substring search indexes and storage tuning (performance)
--
-- Searches use ILIKE '%term%', which a normal B-tree index can't serve.
-- Trigram (pg_trgm) GIN indexes make them index lookups instead of full scans.
-- Every column in a searched OR needs one, or the planner falls back to a scan.

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS "IX_TrackedItems_TagCode_trgm" ON "TrackedItems" USING gin ("TagCode" gin_trgm_ops);
CREATE INDEX IF NOT EXISTS "IX_TrackedItems_Name_trgm" ON "TrackedItems" USING gin ("Name" gin_trgm_ops);
CREATE INDEX IF NOT EXISTS "IX_TrackedItems_Description_trgm" ON "TrackedItems" USING gin ("Description" gin_trgm_ops);

CREATE INDEX IF NOT EXISTS "IX_Shipments_TrackingNumber_trgm" ON "Shipments" USING gin ("TrackingNumber" gin_trgm_ops);
CREATE INDEX IF NOT EXISTS "IX_Shipments_Reference_trgm" ON "Shipments" USING gin ("Reference" gin_trgm_ops);
CREATE INDEX IF NOT EXISTS "IX_ShipmentLegs_DocumentNumber_trgm" ON "ShipmentLegs" USING gin ("DocumentNumber" gin_trgm_ops);
CREATE INDEX IF NOT EXISTS "IX_ShipmentLegs_VoyageNumber_trgm" ON "ShipmentLegs" USING gin ("VoyageNumber" gin_trgm_ops);
-- The old B-tree index on DocumentNumber was only used by the ILIKE search,
-- which the trigram index now serves.
DROP INDEX IF EXISTS "IX_ShipmentLegs_DocumentNumber";

-- TrackedItems is updated on every tracking event (status/location/LastEventAt).
-- Leaving 10% free space per page lets PostgreSQL do in-page (HOT) updates, and
-- more frequent autovacuum keeps dead row versions from bloating the table.
ALTER TABLE "TrackedItems" SET (fillfactor = 90, autovacuum_vacuum_scale_factor = 0.05, autovacuum_analyze_scale_factor = 0.02);

ANALYZE "TrackedItems";
ANALYZE "Shipments";
ANALYZE "ShipmentLegs";
