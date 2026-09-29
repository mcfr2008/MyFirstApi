-- Feature: Link TrackedItems to master data
-- Adds category / current location / owner references to TrackedItems and
-- migrates the old free-text "Category" column into ItemCategories.

ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "CategoryId" INT REFERENCES "ItemCategories"("Id");
ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "CurrentLocationId" INT REFERENCES "Locations"("Id");
ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "OwnerPartyId" INT REFERENCES "Parties"("Id");

CREATE INDEX IF NOT EXISTS "IX_TrackedItems_CategoryId" ON "TrackedItems" ("CategoryId");
CREATE INDEX IF NOT EXISTS "IX_TrackedItems_CurrentLocationId" ON "TrackedItems" ("CurrentLocationId");
CREATE INDEX IF NOT EXISTS "IX_TrackedItems_OwnerPartyId" ON "TrackedItems" ("OwnerPartyId");

-- One-time migration (only runs while the old column still exists):
-- each distinct free-text category becomes an ItemCategories row.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'TrackedItems' AND column_name = 'Category'
    ) THEN
        CREATE TEMP TABLE category_map ON COMMIT DROP AS
        SELECT DISTINCT
            TRIM("Category") AS name,
            -- e.g. 'Frozen food' -> 'FROZEN_FOOD'; non-Latin names get a hash-based code.
            COALESCE(
                NULLIF(LEFT(UPPER(TRIM(BOTH '_' FROM REGEXP_REPLACE(TRIM("Category"), '[^A-Za-z0-9]+', '_', 'g'))), 50), ''),
                'CAT_' || UPPER(LEFT(MD5(TRIM("Category")), 8))
            ) AS code
        FROM "TrackedItems"
        WHERE "Category" IS NOT NULL AND TRIM("Category") <> '';

        INSERT INTO "ItemCategories" ("Code", "Name")
        SELECT DISTINCT ON (code) code, name FROM category_map ORDER BY code, name
        ON CONFLICT ("Code") DO NOTHING;

        UPDATE "TrackedItems" t
        SET "CategoryId" = c."Id"
        FROM category_map m
        JOIN "ItemCategories" c ON c."Code" = m.code
        WHERE t."CategoryId" IS NULL AND TRIM(t."Category") = m.name;

        ALTER TABLE "TrackedItems" DROP COLUMN "Category";
    END IF;
END $$;
