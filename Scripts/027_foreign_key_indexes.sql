-- Feature: Indexes for foreign-key columns (performance)
--
-- PostgreSQL doesn't index foreign-key columns automatically. Without an index,
-- deleting or re-keying a referenced row (e.g. removing a planned shipment leg)
-- scans the whole referencing table to check for references - on TrackingEvents
-- that means millions of rows per deleted row. Found while deleting load-test data.
-- Check for new gaps with Scripts/maintenance/health_check.sql (section 6).

CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ShipmentLegId" ON "TrackingEvents" ("ShipmentLegId") WHERE "ShipmentLegId" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_ShipmentLegs_OriginLocationId" ON "ShipmentLegs" ("OriginLocationId");
CREATE INDEX IF NOT EXISTS "IX_ShipmentLegs_DestinationLocationId" ON "ShipmentLegs" ("DestinationLocationId");
CREATE INDEX IF NOT EXISTS "IX_ShipmentLegs_CarrierId" ON "ShipmentLegs" ("CarrierId") WHERE "CarrierId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_ShipmentLegs_VehicleId" ON "ShipmentLegs" ("VehicleId") WHERE "VehicleId" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_Shipments_OriginLocationId" ON "Shipments" ("OriginLocationId");
CREATE INDEX IF NOT EXISTS "IX_Shipments_DestinationLocationId" ON "Shipments" ("DestinationLocationId");

CREATE INDEX IF NOT EXISTS "IX_Containers_CurrentLocationId" ON "Containers" ("CurrentLocationId") WHERE "CurrentLocationId" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");
