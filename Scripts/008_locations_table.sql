-- Feature: Master data - locations
-- Schema for MyFirstApi.Models.Location / LocationsController.
-- Warehouses, hubs, branches, drop points and customer addresses items move between.

CREATE TABLE IF NOT EXISTS "Locations" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    -- Stored as the LocationType enum name (Warehouse, Hub, Branch, ...).
    "Type" VARCHAR(30) NOT NULL,
    "AddressLine" VARCHAR(500),
    "SubDistrict" VARCHAR(100),
    "District" VARCHAR(100),
    "Province" VARCHAR(100),
    "PostalCode" VARCHAR(20),
    "Country" VARCHAR(2) NOT NULL DEFAULT 'TH',
    "Latitude" NUMERIC(9, 6),
    "Longitude" NUMERIC(9, 6),
    "ContactName" VARCHAR(255),
    "ContactPhone" VARCHAR(30),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_Locations_Code" UNIQUE ("Code")
);

CREATE INDEX IF NOT EXISTS "IX_Locations_Type" ON "Locations" ("Type");
CREATE INDEX IF NOT EXISTS "IX_Locations_Province" ON "Locations" ("Province");
