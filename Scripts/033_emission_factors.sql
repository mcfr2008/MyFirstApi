-- Feature: Carbon footprint - emission factors (gCO2e per tonne-km, well-to-wheel)
-- Schema + defaults for MyFirstApi.Models.EmissionFactor / EmissionFactorsController,
-- used by Services/CarbonFootprintService.cs (GET /api/v1/Shipments/{id}/emissions).

CREATE TABLE IF NOT EXISTS "EmissionFactors" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Mode" VARCHAR(20) NOT NULL,
    "CarrierId" INT REFERENCES "Carriers"("Id"),
    "GramsCo2ePerTonneKm" NUMERIC(10, 3) NOT NULL,
    "Source" VARCHAR(500),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_EmissionFactors_Code" UNIQUE ("Code"),
    CONSTRAINT "CK_EmissionFactors_NotNegative" CHECK ("GramsCo2ePerTonneKm" >= 0)
);

-- One default per mode (CarrierId NULL) and one per (mode, carrier).
CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmissionFactors_Mode_Carrier"
    ON "EmissionFactors" ("Mode", COALESCE("CarrierId", 0));
CREATE INDEX IF NOT EXISTS "IX_EmissionFactors_CarrierId" ON "EmissionFactors" ("CarrierId") WHERE "CarrierId" IS NOT NULL;

-- Indicative mode defaults (typical well-to-wheel intensities for general freight).
-- Replace them with values from the GLEC Framework default tables or your carriers'
-- reported intensities before using the results for official reporting.
INSERT INTO "EmissionFactors" ("Code", "Name", "Mode", "GramsCo2ePerTonneKm", "Source") VALUES
    ('ROAD-DEFAULT',    'Road freight, truck (average load)',   'Road',    100, 'Indicative default - replace with GLEC Framework / carrier value'),
    ('COURIER-DEFAULT', 'Courier / last mile, light van',       'Courier', 500, 'Indicative default - replace with GLEC Framework / carrier value'),
    ('RAIL-DEFAULT',    'Rail freight (diesel/electric mix)',   'Rail',     25, 'Indicative default - replace with GLEC Framework / carrier value'),
    ('AIR-DEFAULT',     'Air freight (belly and freighter mix)','Air',    1000, 'Indicative default - replace with GLEC Framework / carrier value'),
    ('SEA-DEFAULT',     'Sea freight, container ship',          'Sea',      15, 'Indicative default - replace with GLEC Framework / carrier value')
ON CONFLICT DO NOTHING;

COMMENT ON TABLE "EmissionFactors" IS 'Greenhouse-gas intensity per transport mode (and optionally per carrier) in gCO2e per tonne-km, well-to-wheel (ISO 14083 / GLEC) | ค่าการปล่อยก๊าซเรือนกระจกต่อรูปแบบการขนส่ง (และต่อผู้ให้บริการ) หน่วยกรัม CO2e ต่อตัน-กิโลเมตร แบบ well-to-wheel (ISO 14083 / GLEC)';
COMMENT ON COLUMN "EmissionFactors"."Id" IS 'Primary key | รหัสหลัก';
COMMENT ON COLUMN "EmissionFactors"."Code" IS 'Unique code, stored upper-case | รหัสไม่ซ้ำ เก็บเป็นตัวพิมพ์ใหญ่';
COMMENT ON COLUMN "EmissionFactors"."Name" IS 'Display name | ชื่อที่แสดง';
COMMENT ON COLUMN "EmissionFactors"."Mode" IS 'Transport mode: Road, Rail, Air, Sea, Courier | รูปแบบการขนส่ง: Road, Rail, Air, Sea, Courier';
COMMENT ON COLUMN "EmissionFactors"."CarrierId" IS 'NULL = default for the mode; set = this carrier''s own value, used for its legs | NULL = ค่าเริ่มต้นของรูปแบบขนส่ง ถ้าระบุ = ค่าเฉพาะของผู้ให้บริการรายนั้น ใช้กับ leg ของผู้ให้บริการนั้น';
COMMENT ON COLUMN "EmissionFactors"."GramsCo2ePerTonneKm" IS 'Well-to-wheel grams CO2e per tonne-kilometre | กรัม CO2e ต่อตัน-กิโลเมตร แบบ well-to-wheel';
COMMENT ON COLUMN "EmissionFactors"."Source" IS 'Where the value comes from (GLEC default table, carrier report, ...) | ที่มาของค่า (ตาราง GLEC, รายงานของผู้ให้บริการ ฯลฯ)';
COMMENT ON COLUMN "EmissionFactors"."IsActive" IS 'Inactive factors are not used in calculations | ค่าที่ปิดใช้งานจะไม่ถูกใช้คำนวณ';
COMMENT ON COLUMN "EmissionFactors"."CreatedAt" IS 'Created at (UTC) | เวลาที่สร้าง (UTC)';
COMMENT ON COLUMN "EmissionFactors"."UpdatedAt" IS 'Last updated at (UTC) | เวลาที่แก้ไขล่าสุด (UTC)';
