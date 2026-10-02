-- Feature: Misroute detection
-- Adds MyFirstApi.Models.TrackingEvent.OffRouteShipmentId. When an event with a
-- location is recorded for an item in an open (Planned / InTransit) shipment that has
-- legs, and the location isn't on that shipment's route (its origin, destination or
-- any leg's origin / destination), the event is flagged with the shipment's id.
-- Set once at record time by Services/TrackingEventRecorder.cs; events are never updated.

ALTER TABLE "TrackingEvents" ADD COLUMN IF NOT EXISTS "OffRouteShipmentId" INT;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TrackingEvents_OffRouteShipmentId') THEN
        ALTER TABLE "TrackingEvents"
            ADD CONSTRAINT "FK_TrackingEvents_OffRouteShipmentId"
            FOREIGN KEY ("OffRouteShipmentId") REFERENCES "Shipments"("Id");
    END IF;
END $$;

-- Foreign-key index, and the lookup for a shipment's off-route scans (rare, so partial).
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_OffRouteShipmentId"
    ON "TrackingEvents" ("OffRouteShipmentId") WHERE "OffRouteShipmentId" IS NOT NULL;

COMMENT ON COLUMN "TrackingEvents"."OffRouteShipmentId" IS 'Set when the event''s location is not on the route of the open shipment the item was in (misroute); NULL = on route or not checked | ระบุเมื่อสถานที่ของเหตุการณ์ไม่อยู่ในเส้นทางของ Shipment ที่สิ่งของอยู่ (ของหลงเส้นทาง) NULL = อยู่ในเส้นทางหรือไม่ได้ตรวจ';
