-- Feature: Master data - vehicles (trucks, trains, aircraft, vessels)
-- Schema for MyFirstApi.Models.Vehicle / VehiclesController.

CREATE TABLE IF NOT EXISTS "Vehicles" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Mode" VARCHAR(20) NOT NULL,
    "CarrierId" INT REFERENCES "Carriers"("Id"),
    "RegistrationNumber" VARCHAR(50),
    "ImoNumber" VARCHAR(7),
    "CapacityKg" NUMERIC(14, 3),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_Vehicles_Code" UNIQUE ("Code")
);

CREATE INDEX IF NOT EXISTS "IX_Vehicles_CarrierId" ON "Vehicles" ("CarrierId");
