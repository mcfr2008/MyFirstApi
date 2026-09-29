-- Feature: Tracked items (Thing-Tag)
-- Schema for MyFirstApi.Models.TrackedItem / TrackedItemsController.
-- One row per physical item carrying a tag (QR code, barcode, RFID, ...).

CREATE TABLE IF NOT EXISTS "TrackedItems" (
    "Id" SERIAL PRIMARY KEY,
    "TagCode" VARCHAR(64) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Description" VARCHAR(2000),
    "Category" VARCHAR(100),
    -- Stored as the ItemStatus enum name (Registered, InTransit, Delivered, ...).
    "Status" VARCHAR(30) NOT NULL DEFAULT 'Registered',
    "WeightKg" NUMERIC(12, 3),
    "LengthCm" NUMERIC(10, 2),
    "WidthCm" NUMERIC(10, 2),
    "HeightCm" NUMERIC(10, 2),
    "DeclaredValue" NUMERIC(18, 2),
    -- Free-form key/value fields for type-specific data
    -- (e.g. serial number for electronics, expiry date for food).
    "Attributes" JSONB NOT NULL DEFAULT '{}',
    -- Archived instead of deleted so an item's tracking history is never lost.
    "IsArchived" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedBy" VARCHAR(255),
    CONSTRAINT "IX_TrackedItems_TagCode" UNIQUE ("TagCode")
);

CREATE INDEX IF NOT EXISTS "IX_TrackedItems_Category" ON "TrackedItems" ("Category");
CREATE INDEX IF NOT EXISTS "IX_TrackedItems_Status" ON "TrackedItems" ("Status");
