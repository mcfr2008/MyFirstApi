-- Feature: Proof of delivery (receiver signature, photos, GPS) + stored files
-- Schema for MyFirstApi.Models.StoredFile / ProofOfDelivery / ProofOfDeliveryPhoto
-- and POST /api/v1/Shipments/{id}/proof-of-delivery.

-- Metadata of uploaded files; the bytes live in file storage (Services/LocalFileStorage.cs).
-- Reusable for other attachments later (B/L, invoices, permits).
CREATE TABLE IF NOT EXISTS "StoredFiles" (
    "Id" UUID PRIMARY KEY,
    "StorageKey" VARCHAR(500) NOT NULL,
    "OriginalFileName" VARCHAR(255),
    "ContentType" VARCHAR(100) NOT NULL,
    "SizeBytes" BIGINT NOT NULL,
    "Sha256" VARCHAR(64) NOT NULL,
    "Purpose" VARCHAR(30) NOT NULL,
    "UploadedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UploadedBy" VARCHAR(255),
    CONSTRAINT "IX_StoredFiles_StorageKey" UNIQUE ("StorageKey")
);

CREATE TABLE IF NOT EXISTS "ProofsOfDelivery" (
    "Id" SERIAL PRIMARY KEY,
    "ShipmentId" INT NOT NULL REFERENCES "Shipments"("Id"),
    "ReceiverName" VARCHAR(255) NOT NULL,
    "ReceiverRelation" VARCHAR(30) NOT NULL,
    "SignatureFileId" UUID NOT NULL REFERENCES "StoredFiles"("Id"),
    "SignedAt" TIMESTAMPTZ NOT NULL,
    "Latitude" NUMERIC(9, 6),
    "Longitude" NUMERIC(9, 6),
    "LocationAccuracyMeters" NUMERIC(10, 2),
    "DeliveredBy" VARCHAR(255),
    "DeviceInfo" VARCHAR(500),
    "Note" VARCHAR(2000),
    "RefusedItems" JSONB NOT NULL DEFAULT '[]',
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    -- One proof of delivery per shipment.
    CONSTRAINT "IX_ProofsOfDelivery_ShipmentId" UNIQUE ("ShipmentId")
);

CREATE INDEX IF NOT EXISTS "IX_ProofsOfDelivery_SignatureFileId" ON "ProofsOfDelivery" ("SignatureFileId");

CREATE TABLE IF NOT EXISTS "ProofOfDeliveryPhotos" (
    "ProofOfDeliveryId" INT NOT NULL REFERENCES "ProofsOfDelivery"("Id") ON DELETE CASCADE,
    "FileId" UUID NOT NULL REFERENCES "StoredFiles"("Id"),
    "SortOrder" INT NOT NULL DEFAULT 0,
    PRIMARY KEY ("ProofOfDeliveryId", "FileId")
);

CREATE INDEX IF NOT EXISTS "IX_ProofOfDeliveryPhotos_FileId" ON "ProofOfDeliveryPhotos" ("FileId");

-- Shipments that need a receiver signature can't use plain /deliver.
ALTER TABLE "Shipments" ADD COLUMN IF NOT EXISTS "RequiresSignature" BOOLEAN NOT NULL DEFAULT TRUE;

-- Comments (English | ไทย)
COMMENT ON TABLE "StoredFiles" IS 'Metadata of uploaded files (signatures, photos, documents); bytes are in file storage | ข้อมูลของไฟล์ที่อัปโหลด (ลายเซ็น รูปถ่าย เอกสาร) ตัวไฟล์เก็บในที่จัดเก็บไฟล์';
COMMENT ON COLUMN "StoredFiles"."Id" IS 'File id (random UUID, used in download URLs) | รหัสไฟล์ (UUID แบบสุ่ม ใช้ในลิงก์ดาวน์โหลด)';
COMMENT ON COLUMN "StoredFiles"."StorageKey" IS 'Path of the file inside file storage | ตำแหน่งไฟล์ในที่จัดเก็บ';
COMMENT ON COLUMN "StoredFiles"."OriginalFileName" IS 'File name sent by the client | ชื่อไฟล์เดิมที่ส่งมา';
COMMENT ON COLUMN "StoredFiles"."ContentType" IS 'MIME type detected from the file content, e.g. image/png | ชนิดไฟล์ที่ตรวจจากเนื้อไฟล์จริง เช่น image/png';
COMMENT ON COLUMN "StoredFiles"."SizeBytes" IS 'File size in bytes | ขนาดไฟล์ (ไบต์)';
COMMENT ON COLUMN "StoredFiles"."Sha256" IS 'SHA-256 of the file content, proves it was not changed later | ค่า SHA-256 ของไฟล์ ใช้พิสูจน์ว่าไฟล์ไม่ถูกแก้ไขภายหลัง';
COMMENT ON COLUMN "StoredFiles"."Purpose" IS 'FilePurpose: Signature, DeliveryPhoto, Document | วัตถุประสงค์: ลายเซ็น รูปถ่ายการส่งมอบ เอกสาร';
COMMENT ON COLUMN "StoredFiles"."UploadedAt" IS 'Uploaded at (UTC) | วันเวลาที่อัปโหลด (UTC)';
COMMENT ON COLUMN "StoredFiles"."UploadedBy" IS 'Username who uploaded | ชื่อผู้ใช้ที่อัปโหลด';

COMMENT ON TABLE "ProofsOfDelivery" IS 'Proof of delivery: receiver signature, photos and GPS at hand-over (one per shipment) | หลักฐานการส่งมอบ: ลายเซ็นผู้รับ รูปถ่าย และพิกัดขณะส่งมอบ (1 รายการต่อ Shipment)';
COMMENT ON COLUMN "ProofsOfDelivery"."Id" IS 'Primary key (auto-generated) | รหัสอ้างอิงหลัก (ระบบสร้างให้)';
COMMENT ON COLUMN "ProofsOfDelivery"."ShipmentId" IS 'Delivered shipment (FK Shipments) | Shipment ที่ส่งมอบ (อ้างอิง Shipments)';
COMMENT ON COLUMN "ProofsOfDelivery"."ReceiverName" IS 'Name of the person who signed | ชื่อผู้ที่เซ็นรับ';
COMMENT ON COLUMN "ProofsOfDelivery"."ReceiverRelation" IS 'ReceiverRelation: Recipient, FamilyMember, Colleague, Reception, Security, Neighbor, Other | ความสัมพันธ์ของผู้เซ็นรับ: ผู้รับเอง ญาติ เพื่อนร่วมงาน พนักงานต้อนรับ รปภ. เพื่อนบ้าน อื่น ๆ';
COMMENT ON COLUMN "ProofsOfDelivery"."SignatureFileId" IS 'Signature image (FK StoredFiles) | รูปลายเซ็น (อ้างอิง StoredFiles)';
COMMENT ON COLUMN "ProofsOfDelivery"."SignedAt" IS 'When the receiver signed (UTC) | เวลาที่ผู้รับเซ็น (UTC)';
COMMENT ON COLUMN "ProofsOfDelivery"."Latitude" IS 'GPS latitude where it was signed | ละติจูด GPS ตำแหน่งที่เซ็นรับ';
COMMENT ON COLUMN "ProofsOfDelivery"."Longitude" IS 'GPS longitude where it was signed | ลองจิจูด GPS ตำแหน่งที่เซ็นรับ';
COMMENT ON COLUMN "ProofsOfDelivery"."LocationAccuracyMeters" IS 'GPS accuracy in meters | ความแม่นยำของ GPS (เมตร)';
COMMENT ON COLUMN "ProofsOfDelivery"."DeliveredBy" IS 'Username of the courier / driver | ชื่อผู้ใช้ของพนักงานส่งของ';
COMMENT ON COLUMN "ProofsOfDelivery"."DeviceInfo" IS 'Device used to capture the signature | อุปกรณ์ที่ใช้เก็บลายเซ็น';
COMMENT ON COLUMN "ProofsOfDelivery"."Note" IS 'Note | หมายเหตุ';
COMMENT ON COLUMN "ProofsOfDelivery"."RefusedItems" IS 'Items the receiver refused (JSON: trackedItemId, tagCode, reasonCode, note) | สิ่งของที่ผู้รับปฏิเสธ (JSON: รหัสสิ่งของ แท็ก รหัสสาเหตุ หมายเหตุ)';
COMMENT ON COLUMN "ProofsOfDelivery"."CreatedAt" IS 'Created at (UTC) | วันเวลาที่สร้าง (UTC)';

COMMENT ON TABLE "ProofOfDeliveryPhotos" IS 'Photos taken at delivery | รูปถ่าย ณ จุดส่งมอบ';
COMMENT ON COLUMN "ProofOfDeliveryPhotos"."ProofOfDeliveryId" IS 'Proof of delivery (FK ProofsOfDelivery) | หลักฐานการส่งมอบ (อ้างอิง ProofsOfDelivery)';
COMMENT ON COLUMN "ProofOfDeliveryPhotos"."FileId" IS 'Photo file (FK StoredFiles) | ไฟล์รูป (อ้างอิง StoredFiles)';
COMMENT ON COLUMN "ProofOfDeliveryPhotos"."SortOrder" IS 'Display order | ลำดับการแสดงผล';

COMMENT ON COLUMN "Shipments"."RequiresSignature" IS 'Delivery needs a receiver signature (proof of delivery) | การส่งมอบต้องมีลายเซ็นผู้รับ (หลักฐานการส่งมอบ)';
