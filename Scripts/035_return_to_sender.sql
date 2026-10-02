-- Feature: Return to sender
-- POST /api/v1/Shipments/{id}/return-to-sender records RETURNED for the shipment's
-- undelivered items, closes an InTransit shipment as ReturnedToSender, and creates a
-- return shipment (receiver -> sender) linked back by ReturnOfShipmentId.
-- Shipments."Status" is VARCHAR(20); 'ReturnedToSender' fits, no type change needed.

ALTER TABLE "Shipments" ADD COLUMN IF NOT EXISTS "ReturnOfShipmentId" INT REFERENCES "Shipments"("Id");

-- One return shipment per shipment; also the foreign-key index.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Shipments_ReturnOfShipmentId"
    ON "Shipments" ("ReturnOfShipmentId") WHERE "ReturnOfShipmentId" IS NOT NULL;

COMMENT ON COLUMN "Shipments"."Status" IS 'ShipmentStatus: Planned, InTransit, Delivered, Cancelled, ReturnedToSender | สถานะ: วางแผน กำลังขนส่ง ส่งสำเร็จ ยกเลิก ตีกลับผู้ส่ง';
COMMENT ON COLUMN "Shipments"."ReturnOfShipmentId" IS 'Set on a return shipment: the original shipment whose undelivered items it brings back to the sender | ระบุใน Shipment ขากลับ: Shipment ต้นฉบับที่นำสิ่งของที่ส่งไม่สำเร็จกลับไปคืนผู้ส่ง';
