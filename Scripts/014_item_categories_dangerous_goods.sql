-- Feature: Dangerous goods classification on item categories (IATA DGR / IMDG).

ALTER TABLE "ItemCategories" ADD COLUMN IF NOT EXISTS "IsDangerousGoods" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "ItemCategories" ADD COLUMN IF NOT EXISTS "UnNumber" VARCHAR(6);
ALTER TABLE "ItemCategories" ADD COLUMN IF NOT EXISTS "HazardClass" VARCHAR(10);
