-- Feature: Multimodal transport - location codes and time zones
-- New LocationType values (Port, Airport, RailStation, ContainerYard,
-- CustomsOffice, BorderCrossing) need no schema change: "Type" is VARCHAR.

ALTER TABLE "Locations" ADD COLUMN IF NOT EXISTS "UnLocode" VARCHAR(5);
ALTER TABLE "Locations" ADD COLUMN IF NOT EXISTS "IataCode" VARCHAR(3);
ALTER TABLE "Locations" ADD COLUMN IF NOT EXISTS "TimeZone" VARCHAR(64) NOT NULL DEFAULT 'Asia/Bangkok';

CREATE INDEX IF NOT EXISTS "IX_Locations_UnLocode" ON "Locations" ("UnLocode");
CREATE INDEX IF NOT EXISTS "IX_Locations_IataCode" ON "Locations" ("IataCode");
