-- Feature: Master data - service areas (which station and hub serve an area)
-- Schema for MyFirstApi.Models.ServiceArea / ServiceAreasController.
-- An area is one postal code, or a whole province (PostalCode NULL). Resolving an
-- address tries its postal code first, then its province.

CREATE TABLE IF NOT EXISTS "ServiceAreas" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Country" VARCHAR(2) NOT NULL DEFAULT 'TH',
    "Province" VARCHAR(100),
    "PostalCode" VARCHAR(10),
    "StationLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    "HubLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_ServiceAreas_Code" UNIQUE ("Code"),
    -- A province-wide area needs its province.
    CONSTRAINT "CK_ServiceAreas_PostalCodeOrProvince" CHECK ("PostalCode" IS NOT NULL OR "Province" IS NOT NULL)
);

-- No overlaps: one area per postal code, one province-wide area per province.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceAreas_Country_PostalCode"
    ON "ServiceAreas" ("Country", "PostalCode") WHERE "PostalCode" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceAreas_Country_Province"
    ON "ServiceAreas" ("Country", lower("Province")) WHERE "PostalCode" IS NULL;

CREATE INDEX IF NOT EXISTS "IX_ServiceAreas_StationLocationId" ON "ServiceAreas" ("StationLocationId");
CREATE INDEX IF NOT EXISTS "IX_ServiceAreas_HubLocationId" ON "ServiceAreas" ("HubLocationId");

COMMENT ON TABLE "ServiceAreas" IS 'Which station serves an area (one postal code or a whole province) and which sorting hub it feeds | พื้นที่ให้บริการ: สาขาที่รับ-ส่งของในพื้นที่ (รหัสไปรษณีย์หรือทั้งจังหวัด) และ hub คัดแยกที่สาขานั้นส่งต่อ';
COMMENT ON COLUMN "ServiceAreas"."Id" IS 'Primary key | รหัสหลัก';
COMMENT ON COLUMN "ServiceAreas"."Code" IS 'Unique code, stored upper-case | รหัสไม่ซ้ำ เก็บเป็นตัวพิมพ์ใหญ่';
COMMENT ON COLUMN "ServiceAreas"."Name" IS 'Display name, e.g. Mueang Chiang Mai | ชื่อพื้นที่ เช่น เมืองเชียงใหม่';
COMMENT ON COLUMN "ServiceAreas"."Country" IS 'ISO 3166-1 alpha-2 country code | รหัสประเทศ ISO 3166-1 alpha-2';
COMMENT ON COLUMN "ServiceAreas"."Province" IS 'Province; required when PostalCode is empty (province-wide area), matched case-insensitively | จังหวัด ต้องระบุเมื่อไม่มีรหัสไปรษณีย์ (ครอบคลุมทั้งจังหวัด) เทียบแบบไม่สนตัวพิมพ์เล็ก-ใหญ่';
COMMENT ON COLUMN "ServiceAreas"."PostalCode" IS 'One postal code (5 digits in Thailand); takes precedence over a province-wide area. NULL = whole province | รหัสไปรษณีย์ (ไทย 5 หลัก) มีลำดับก่อนพื้นที่ระดับจังหวัด NULL = ทั้งจังหวัด';
COMMENT ON COLUMN "ServiceAreas"."StationLocationId" IS 'Station that picks up and delivers in the area (Branch, DropPoint, Hub or Warehouse) | สาขาที่รับและส่งของในพื้นที่ (Branch, DropPoint, Hub หรือ Warehouse)';
COMMENT ON COLUMN "ServiceAreas"."HubLocationId" IS 'Sorting hub the station sends to and receives from (type Hub) | hub คัดแยกที่สาขาส่งของไปและรับของมา (ประเภท Hub)';
COMMENT ON COLUMN "ServiceAreas"."IsActive" IS 'Inactive areas are skipped when resolving an address | พื้นที่ที่ปิดใช้งานจะไม่ถูกใช้ตอนค้นหาพื้นที่ของที่อยู่';
COMMENT ON COLUMN "ServiceAreas"."CreatedAt" IS 'Created at (UTC) | เวลาที่สร้าง (UTC)';
COMMENT ON COLUMN "ServiceAreas"."UpdatedAt" IS 'Last updated at (UTC) | เวลาที่แก้ไขล่าสุด (UTC)';
