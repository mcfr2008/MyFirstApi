-- Feature: Automatic return to sender after N failed delivery attempts
-- When DELIVERY_FAILED is recorded on an InTransit shipment (POST /Shipments/{id}/events)
-- and the shipment reaches its attempt limit, it is returned to sender automatically
-- with reason MAX_ATTEMPTS_REACHED (Services/ShipmentService.Return.cs).
-- Limit: Shipments."MaxDeliveryAttempts", else Returns:MaxDeliveryAttempts in appsettings (default 3).

ALTER TABLE "Shipments" ADD COLUMN IF NOT EXISTS "MaxDeliveryAttempts" INT;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Shipments_MaxDeliveryAttempts') THEN
        ALTER TABLE "Shipments" ADD CONSTRAINT "CK_Shipments_MaxDeliveryAttempts"
            CHECK ("MaxDeliveryAttempts" IS NULL OR "MaxDeliveryAttempts" BETWEEN 0 AND 10);
    END IF;
END $$;

INSERT INTO "ReasonCodes" ("Code", "NameTh", "NameEn", "EventTypeCodes", "RequiresNote", "SortOrder") VALUES
    ('MAX_ATTEMPTS_REACHED', 'นำส่งไม่สำเร็จครบจำนวนครั้งที่กำหนด', 'Delivery attempts exhausted', '{RETURNED}', FALSE, 60)
ON CONFLICT DO NOTHING;

COMMENT ON COLUMN "Shipments"."MaxDeliveryAttempts" IS 'Failed delivery attempts before an automatic return to sender; NULL = system default (Returns:MaxDeliveryAttempts), 0 = never return automatically | จำนวนครั้งที่นำส่งไม่สำเร็จก่อนตีกลับผู้ส่งอัตโนมัติ NULL = ค่าเริ่มต้นของระบบ 0 = ไม่ตีกลับอัตโนมัติ';
