-- Feature: Master data - carriers
-- Schema for MyFirstApi.Models.Carrier / CarriersController.

CREATE TABLE IF NOT EXISTS "Carriers" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    -- TransportMode enum names (Road, Rail, Air, Sea, Courier).
    "Modes" TEXT[] NOT NULL DEFAULT '{}',
    "ScacCode" VARCHAR(4),
    "IataCode" VARCHAR(3),
    "ContactName" VARCHAR(255),
    "Phone" VARCHAR(30),
    "Email" VARCHAR(255),
    "Website" VARCHAR(500),
    "TrackingUrlTemplate" VARCHAR(500),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_Carriers_Code" UNIQUE ("Code")
);
