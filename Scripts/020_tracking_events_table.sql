-- Feature: Tracking events (append-only item history)
-- Schema for MyFirstApi.Models.TrackingEvent / TrackingEventsController.

CREATE TABLE IF NOT EXISTS "TrackingEvents" (
    "Id" BIGSERIAL PRIMARY KEY,
    "TrackedItemId" INT NOT NULL REFERENCES "TrackedItems"("Id"),
    "EventTypeId" INT NOT NULL REFERENCES "EventTypes"("Id"),
    "LocationId" INT REFERENCES "Locations"("Id"),
    "OccurredAt" TIMESTAMPTZ NOT NULL,
    "RecordedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "RecordedBy" VARCHAR(255),
    -- EventSource enum name (Manual, Scan, Container, Shipment).
    "Source" VARCHAR(20) NOT NULL,
    "Note" VARCHAR(2000),
    "Latitude" NUMERIC(9, 6),
    "Longitude" NUMERIC(9, 6),
    "ShipmentId" INT REFERENCES "Shipments"("Id"),
    "ShipmentLegId" INT REFERENCES "ShipmentLegs"("Id"),
    "ContainerId" INT REFERENCES "Containers"("Id")
);

-- Timeline lookups: an item's history in time order.
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_TrackedItemId_OccurredAt" ON "TrackingEvents" ("TrackedItemId", "OccurredAt");
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_ShipmentId" ON "TrackingEvents" ("ShipmentId");
CREATE INDEX IF NOT EXISTS "IX_TrackingEvents_LocationId_OccurredAt" ON "TrackingEvents" ("LocationId", "OccurredAt");
