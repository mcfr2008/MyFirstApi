-- Feature: Master data - parties
-- Schema for MyFirstApi.Models.Party / PartiesController.
-- Senders, receivers, customers and item owners.

CREATE TABLE IF NOT EXISTS "Parties" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    -- Stored as the PartyType enum name (Individual, Company).
    "Type" VARCHAR(30) NOT NULL,
    "TaxId" VARCHAR(20),
    "ContactName" VARCHAR(255),
    "Phone" VARCHAR(30),
    "Email" VARCHAR(255),
    "AddressLine" VARCHAR(500),
    "SubDistrict" VARCHAR(100),
    "District" VARCHAR(100),
    "Province" VARCHAR(100),
    "PostalCode" VARCHAR(20),
    "Country" VARCHAR(2) NOT NULL DEFAULT 'TH',
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_Parties_Code" UNIQUE ("Code")
);

CREATE INDEX IF NOT EXISTS "IX_Parties_Type" ON "Parties" ("Type");
