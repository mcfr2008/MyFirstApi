-- Feature: Shipments with multimodal legs
-- Schema for MyFirstApi.Models.Shipment / ShipmentItem / ShipmentLeg / ShipmentsController.

CREATE TABLE IF NOT EXISTS "Shipments" (
    "Id" SERIAL PRIMARY KEY,
    "TrackingNumber" VARCHAR(64) NOT NULL,
    "Reference" VARCHAR(100),
    "SenderPartyId" INT NOT NULL REFERENCES "Parties"("Id"),
    "ReceiverPartyId" INT NOT NULL REFERENCES "Parties"("Id"),
    "OriginLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    "DestinationLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    -- ShipmentStatus enum name (Planned, InTransit, Delivered, Cancelled).
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Planned',
    "Incoterm" VARCHAR(3),
    -- CustomsStatus enum name (NotRequired, Pending, InProgress, Hold, Cleared).
    "CustomsStatus" VARCHAR(20) NOT NULL DEFAULT 'NotRequired',
    "PlannedPickupAt" TIMESTAMPTZ,
    "DeliveredAt" TIMESTAMPTZ,
    "Notes" VARCHAR(2000),
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedBy" VARCHAR(255),
    CONSTRAINT "IX_Shipments_TrackingNumber" UNIQUE ("TrackingNumber")
);

CREATE INDEX IF NOT EXISTS "IX_Shipments_Status" ON "Shipments" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Shipments_SenderPartyId" ON "Shipments" ("SenderPartyId");
CREATE INDEX IF NOT EXISTS "IX_Shipments_ReceiverPartyId" ON "Shipments" ("ReceiverPartyId");

CREATE TABLE IF NOT EXISTS "ShipmentItems" (
    "ShipmentId" INT NOT NULL REFERENCES "Shipments"("Id") ON DELETE CASCADE,
    "TrackedItemId" INT NOT NULL REFERENCES "TrackedItems"("Id"),
    "AddedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("ShipmentId", "TrackedItemId")
);

CREATE INDEX IF NOT EXISTS "IX_ShipmentItems_TrackedItemId" ON "ShipmentItems" ("TrackedItemId");

CREATE TABLE IF NOT EXISTS "ShipmentLegs" (
    "Id" SERIAL PRIMARY KEY,
    "ShipmentId" INT NOT NULL REFERENCES "Shipments"("Id") ON DELETE CASCADE,
    "Sequence" INT NOT NULL,
    "Mode" VARCHAR(20) NOT NULL,
    "CarrierId" INT REFERENCES "Carriers"("Id"),
    "VehicleId" INT REFERENCES "Vehicles"("Id"),
    "VehicleName" VARCHAR(255),
    "VoyageNumber" VARCHAR(50),
    "OriginLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    "DestinationLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    "PlannedDeparture" TIMESTAMPTZ,
    "PlannedArrival" TIMESTAMPTZ,
    "ActualDeparture" TIMESTAMPTZ,
    "ActualArrival" TIMESTAMPTZ,
    "DocumentType" VARCHAR(30),
    "DocumentNumber" VARCHAR(100),
    CONSTRAINT "IX_ShipmentLegs_ShipmentId_Sequence" UNIQUE ("ShipmentId", "Sequence")
);

CREATE INDEX IF NOT EXISTS "IX_ShipmentLegs_DocumentNumber" ON "ShipmentLegs" ("DocumentNumber");
