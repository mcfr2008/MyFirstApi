-- Feature: Customer returns (RMA - Return Merchandise Authorization)
-- Schema for MyFirstApi.Models.ReturnRequest / ReturnRequestItem and /api/v1/Returns.
-- A receiver asks to send delivered items back (damaged, wrong item, ...). The request
-- is approved or rejected; approval creates a return shipment (receiver -> sender).
-- "Received" / "Cancelled" after approval follow the return shipment (Delivered / Cancelled).

CREATE TABLE IF NOT EXISTS "ReturnRequests" (
    "Id" SERIAL PRIMARY KEY,
    "RmaNumber" VARCHAR(30) NOT NULL,
    "ShipmentId" INT NOT NULL REFERENCES "Shipments"("Id"),
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Requested',
    "Note" VARCHAR(2000),
    "RequestedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "RequestedBy" VARCHAR(255),
    "DecidedAt" TIMESTAMPTZ,
    "DecidedBy" VARCHAR(255),
    "DecisionNote" VARCHAR(2000),
    "ReturnShipmentId" INT REFERENCES "Shipments"("Id"),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_ReturnRequests_RmaNumber" UNIQUE ("RmaNumber")
);

CREATE INDEX IF NOT EXISTS "IX_ReturnRequests_ShipmentId" ON "ReturnRequests" ("ShipmentId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReturnRequests_ReturnShipmentId"
    ON "ReturnRequests" ("ReturnShipmentId") WHERE "ReturnShipmentId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_ReturnRequests_Status" ON "ReturnRequests" ("Status");

CREATE TABLE IF NOT EXISTS "ReturnRequestItems" (
    "ReturnRequestId" INT NOT NULL REFERENCES "ReturnRequests"("Id") ON DELETE CASCADE,
    "TrackedItemId" INT NOT NULL REFERENCES "TrackedItems"("Id"),
    "ReasonCodeId" INT NOT NULL REFERENCES "ReasonCodes"("Id"),
    "Note" VARCHAR(2000),
    PRIMARY KEY ("ReturnRequestId", "TrackedItemId")
);

CREATE INDEX IF NOT EXISTS "IX_ReturnRequestItems_TrackedItemId" ON "ReturnRequestItems" ("TrackedItemId");
CREATE INDEX IF NOT EXISTS "IX_ReturnRequestItems_ReasonCodeId" ON "ReturnRequestItems" ("ReasonCodeId");

-- Informational event on each item when a return is requested (needs a reason).
INSERT INTO "EventTypes" ("Code", "NameTh", "NameEn", "ResultingStatus", "IsTerminal", "RequiresReason", "SortOrder") VALUES
    ('RETURN_REQUESTED', 'ผู้รับแจ้งขอคืนสินค้า', 'Return requested by receiver', NULL, FALSE, TRUE, 85)
ON CONFLICT DO NOTHING;

INSERT INTO "ReasonCodes" ("Code", "NameTh", "NameEn", "EventTypeCodes", "RequiresNote", "SortOrder") VALUES
    ('DAMAGED_ON_ARRIVAL', 'สินค้าเสียหายเมื่อได้รับ',      'Damaged on arrival',     '{RETURN_REQUESTED}', FALSE, 300),
    ('WRONG_ITEM',         'ได้รับสินค้าผิด',               'Wrong item received',    '{RETURN_REQUESTED}', FALSE, 310),
    ('DEFECTIVE',          'สินค้าชำรุด/ใช้งานไม่ได้',        'Defective',              '{RETURN_REQUESTED}', FALSE, 320),
    ('NOT_AS_DESCRIBED',   'สินค้าไม่ตรงตามที่ระบุ',         'Not as described',       '{RETURN_REQUESTED}', FALSE, 330),
    ('NO_LONGER_NEEDED',   'ไม่ต้องการสินค้าแล้ว',           'No longer needed',       '{RETURN_REQUESTED}', FALSE, 340)
ON CONFLICT DO NOTHING;

COMMENT ON TABLE "ReturnRequests" IS 'Customer return requests (RMA) for delivered items; approval creates a return shipment | คำขอคืนสินค้าของลูกค้า (RMA) สำหรับสิ่งของที่ส่งถึงแล้ว เมื่ออนุมัติจะสร้าง Shipment ขากลับ';
COMMENT ON COLUMN "ReturnRequests"."Id" IS 'Primary key | รหัสหลัก';
COMMENT ON COLUMN "ReturnRequests"."RmaNumber" IS 'Return authorization number given to the customer, e.g. RMA2610039K4Q7X | เลขอนุมัติคืนสินค้าที่ให้ลูกค้า';
COMMENT ON COLUMN "ReturnRequests"."ShipmentId" IS 'The delivered shipment the items came in | Shipment ที่ส่งสิ่งของมาถึงผู้รับ';
COMMENT ON COLUMN "ReturnRequests"."Status" IS 'Stored status: Requested, Approved, Rejected, Cancelled (Received is derived from the return shipment) | สถานะ: Requested ขอคืน, Approved อนุมัติ, Rejected ปฏิเสธ, Cancelled ยกเลิก (Received ได้รับคืนแล้ว คำนวณจาก Shipment ขากลับ)';
COMMENT ON COLUMN "ReturnRequests"."Note" IS 'Customer''s explanation | คำอธิบายจากลูกค้า';
COMMENT ON COLUMN "ReturnRequests"."RequestedAt" IS 'When the return was requested (UTC) | เวลาที่ขอคืน (UTC)';
COMMENT ON COLUMN "ReturnRequests"."RequestedBy" IS 'User who entered the request | ผู้ใช้ที่บันทึกคำขอ';
COMMENT ON COLUMN "ReturnRequests"."DecidedAt" IS 'When it was approved, rejected or cancelled (UTC) | เวลาที่อนุมัติ ปฏิเสธ หรือยกเลิก (UTC)';
COMMENT ON COLUMN "ReturnRequests"."DecidedBy" IS 'User who decided | ผู้ใช้ที่ตัดสิน';
COMMENT ON COLUMN "ReturnRequests"."DecisionNote" IS 'Why it was rejected or cancelled | เหตุผลที่ปฏิเสธหรือยกเลิก';
COMMENT ON COLUMN "ReturnRequests"."ReturnShipmentId" IS 'Return shipment created on approval | Shipment ขากลับที่สร้างเมื่ออนุมัติ';
COMMENT ON COLUMN "ReturnRequests"."UpdatedAt" IS 'Last updated at (UTC) | เวลาที่แก้ไขล่าสุด (UTC)';
COMMENT ON TABLE "ReturnRequestItems" IS 'Items in a return request, each with its own reason | สิ่งของในคำขอคืน แต่ละชิ้นมีเหตุผลของตัวเอง';
COMMENT ON COLUMN "ReturnRequestItems"."ReturnRequestId" IS 'The return request | คำขอคืนสินค้า';
COMMENT ON COLUMN "ReturnRequestItems"."TrackedItemId" IS 'Delivered item to send back | สิ่งของที่ส่งถึงแล้วและจะส่งคืน';
COMMENT ON COLUMN "ReturnRequestItems"."ReasonCodeId" IS 'Why it is returned (a reason allowed for RETURN_REQUESTED) | เหตุผลที่คืน (ต้องใช้กับ RETURN_REQUESTED ได้)';
COMMENT ON COLUMN "ReturnRequestItems"."Note" IS 'Details for this item | รายละเอียดของสิ่งของชิ้นนี้';
