-- Feature: Master data - lanes (scheduled transport connections between network points)
-- Schema for MyFirstApi.Models.Lane / LanesController.
-- One-way: station -> hub, hub -> hub (linehaul), hub -> station, port -> port, ...
-- The route planner chains lanes from the origin's hub to the destination's hub.

CREATE TABLE IF NOT EXISTS "Lanes" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "OriginLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    "DestinationLocationId" INT NOT NULL REFERENCES "Locations"("Id"),
    "Mode" VARCHAR(20) NOT NULL,
    "CarrierId" INT REFERENCES "Carriers"("Id"),
    "TransitTimeMinutes" INT NOT NULL,
    "DistanceKm" NUMERIC(8, 1),
    "DepartureTimes" TEXT[] NOT NULL DEFAULT '{}',
    "OperatingDays" TEXT[] NOT NULL DEFAULT '{}',
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_Lanes_Code" UNIQUE ("Code"),
    CONSTRAINT "CK_Lanes_DifferentEnds" CHECK ("OriginLocationId" <> "DestinationLocationId"),
    CONSTRAINT "CK_Lanes_TransitTime" CHECK ("TransitTimeMinutes" > 0)
);

-- Route search walks lanes out of each location.
CREATE INDEX IF NOT EXISTS "IX_Lanes_OriginLocationId" ON "Lanes" ("OriginLocationId");
CREATE INDEX IF NOT EXISTS "IX_Lanes_DestinationLocationId" ON "Lanes" ("DestinationLocationId");
CREATE INDEX IF NOT EXISTS "IX_Lanes_CarrierId" ON "Lanes" ("CarrierId") WHERE "CarrierId" IS NOT NULL;

COMMENT ON TABLE "Lanes" IS 'One-way scheduled transport connections between network points (station-hub, hub-hub linehaul, port-port), chained by the route planner | เส้นทางวิ่งตามตารางระหว่างจุดในเครือข่าย (สาขา-hub, hub-hub, ท่าเรือ-ท่าเรือ) แบบทางเดียว ใช้ต่อกันเป็นเส้นทางขนส่ง';
COMMENT ON COLUMN "Lanes"."Id" IS 'Primary key | รหัสหลัก';
COMMENT ON COLUMN "Lanes"."Code" IS 'Unique code, stored upper-case | รหัสไม่ซ้ำ เก็บเป็นตัวพิมพ์ใหญ่';
COMMENT ON COLUMN "Lanes"."Name" IS 'Display name, e.g. Lat Krabang - Chiang Mai linehaul | ชื่อเส้นทาง เช่น ลาดกระบัง - เชียงใหม่';
COMMENT ON COLUMN "Lanes"."OriginLocationId" IS 'Where the lane departs (any location type except CustomerAddress) | จุดต้นทาง (ประเภทใดก็ได้ยกเว้น CustomerAddress)';
COMMENT ON COLUMN "Lanes"."DestinationLocationId" IS 'Where the lane arrives (any location type except CustomerAddress) | จุดปลายทาง (ประเภทใดก็ได้ยกเว้น CustomerAddress)';
COMMENT ON COLUMN "Lanes"."Mode" IS 'Transport mode: Road, Rail, Air, Sea, Courier | รูปแบบการขนส่ง: Road, Rail, Air, Sea, Courier';
COMMENT ON COLUMN "Lanes"."CarrierId" IS 'Carrier running the lane; must operate its mode (Road and Courier count as one) | ผู้ให้บริการขนส่งที่วิ่งเส้นทางนี้ ต้องให้บริการรูปแบบนี้ (Road กับ Courier นับเป็นกลุ่มเดียวกัน)';
COMMENT ON COLUMN "Lanes"."TransitTimeMinutes" IS 'Minutes from departure at the origin to arrival at the destination | เวลาเดินทาง (นาที) จากออกต้นทางถึงปลายทาง';
COMMENT ON COLUMN "Lanes"."DistanceKm" IS 'Distance in kilometres | ระยะทาง (กิโลเมตร)';
COMMENT ON COLUMN "Lanes"."DepartureTimes" IS 'Scheduled departures "HH:mm" in the origin location''s time zone, sorted; empty = any time (on demand) | เวลาออกตามตาราง "HH:mm" ตามเขตเวลาของต้นทาง เรียงลำดับ ว่าง = ออกได้ทุกเวลา';
COMMENT ON COLUMN "Lanes"."OperatingDays" IS 'Days the lane runs: Monday ... Sunday; empty = every day | วันที่วิ่ง: Monday ... Sunday ว่าง = ทุกวัน';
COMMENT ON COLUMN "Lanes"."IsActive" IS 'Inactive lanes are not used for new routes | เส้นทางที่ปิดใช้งานจะไม่ถูกใช้วางเส้นทางใหม่';
COMMENT ON COLUMN "Lanes"."CreatedAt" IS 'Created at (UTC) | เวลาที่สร้าง (UTC)';
COMMENT ON COLUMN "Lanes"."UpdatedAt" IS 'Last updated at (UTC) | เวลาที่แก้ไขล่าสุด (UTC)';
