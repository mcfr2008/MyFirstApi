-- Feature: Master data - item categories
-- Schema for MyFirstApi.Models.ItemCategory / ItemCategoriesController.

CREATE TABLE IF NOT EXISTS "ItemCategories" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Description" VARCHAR(2000),
    -- Attribute keys every TrackedItem in this category must fill in.
    "RequiredAttributes" TEXT[] NOT NULL DEFAULT '{}',
    "IsFragile" BOOLEAN NOT NULL DEFAULT FALSE,
    "RequiresTemperatureControl" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_ItemCategories_Code" UNIQUE ("Code")
);
