-- Feature: Tracked items - customs data, container membership, last event time.

ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "HsCode" VARCHAR(12);
ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "OriginCountry" VARCHAR(2);
ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "Currency" VARCHAR(3);
ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "CurrentContainerId" INT REFERENCES "Containers"("Id");
ALTER TABLE "TrackedItems" ADD COLUMN IF NOT EXISTS "LastEventAt" TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS "IX_TrackedItems_CurrentContainerId" ON "TrackedItems" ("CurrentContainerId");

-- Items created before this script had a value without a currency.
UPDATE "TrackedItems" SET "Currency" = 'THB' WHERE "DeclaredValue" IS NOT NULL AND "Currency" IS NULL;
