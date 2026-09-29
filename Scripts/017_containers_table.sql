-- Feature: Containers / consolidation (pallets, ISO containers, air ULDs)
-- Schema for MyFirstApi.Models.Container / ContainersController.

CREATE TABLE IF NOT EXISTS "Containers" (
    "Id" SERIAL PRIMARY KEY,
    -- ISO 6346 number for shipping containers (validated in code), any code otherwise.
    "Code" VARCHAR(50) NOT NULL,
    "Type" VARCHAR(30) NOT NULL,
    "SealNumber" VARCHAR(50),
    -- Nesting: a pallet inside a container, etc.
    "ParentContainerId" INT REFERENCES "Containers"("Id"),
    "CurrentLocationId" INT REFERENCES "Locations"("Id"),
    "MaxPayloadKg" NUMERIC(14, 3),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_Containers_Code" UNIQUE ("Code")
);

CREATE INDEX IF NOT EXISTS "IX_Containers_ParentContainerId" ON "Containers" ("ParentContainerId");
