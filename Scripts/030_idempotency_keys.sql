-- Feature: Idempotency keys (safe retries of POST requests)
-- Schema for MyFirstApi.Models.IdempotencyKey, used by Idempotency/IdempotencyFilter.cs.
-- A client sends "Idempotency-Key: <unique value>" on a POST; a retry with the same key
-- gets the stored response instead of running the action again (no duplicate events).

CREATE TABLE IF NOT EXISTS "IdempotencyKeys" (
    "Username" VARCHAR(255) NOT NULL,
    "Key" VARCHAR(255) NOT NULL,
    "RequestMethod" VARCHAR(10) NOT NULL,
    "RequestPath" VARCHAR(500) NOT NULL,
    "RequestHash" VARCHAR(64) NOT NULL,
    "ResponseStatusCode" INT,
    "ResponseBody" TEXT,
    "ResponseLocation" VARCHAR(1000),
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ExpiresAt" TIMESTAMPTZ NOT NULL,
    -- Keys are scoped to the user who sent them.
    PRIMARY KEY ("Username", "Key")
);

-- Hourly cleanup of expired keys (Services/IdempotencyKeyCleanupService.cs).
CREATE INDEX IF NOT EXISTS "IX_IdempotencyKeys_ExpiresAt" ON "IdempotencyKeys" ("ExpiresAt");

COMMENT ON TABLE "IdempotencyKeys" IS 'Responses of POST requests sent with an Idempotency-Key header, replayed when the same request is retried | คำตอบของคำขอ POST ที่ส่งมาพร้อม header Idempotency-Key ใช้ตอบซ้ำเมื่อส่งคำขอเดิมอีกครั้ง';
COMMENT ON COLUMN "IdempotencyKeys"."Username" IS 'User who sent the request (keys are per user) | ผู้ใช้ที่ส่งคำขอ (key แยกตามผู้ใช้)';
COMMENT ON COLUMN "IdempotencyKeys"."Key" IS 'Value of the Idempotency-Key header, chosen by the client | ค่าของ header Idempotency-Key ที่ client กำหนด';
COMMENT ON COLUMN "IdempotencyKeys"."RequestMethod" IS 'HTTP method of the original request | HTTP method ของคำขอแรก';
COMMENT ON COLUMN "IdempotencyKeys"."RequestPath" IS 'Path and query string of the original request | path และ query string ของคำขอแรก';
COMMENT ON COLUMN "IdempotencyKeys"."RequestHash" IS 'SHA-256 of method, path and request data; a retry with a different request is rejected | ค่า SHA-256 ของ method, path และข้อมูลคำขอ หากส่งคำขอต่างจากเดิมด้วย key เดิมจะถูกปฏิเสธ';
COMMENT ON COLUMN "IdempotencyKeys"."ResponseStatusCode" IS 'HTTP status of the stored (successful) response | HTTP status ของคำตอบที่บันทึกไว้ (สำเร็จ)';
COMMENT ON COLUMN "IdempotencyKeys"."ResponseBody" IS 'JSON body of the stored response, replayed as-is | เนื้อหา JSON ของคำตอบที่บันทึกไว้ ส่งกลับตามเดิม';
COMMENT ON COLUMN "IdempotencyKeys"."ResponseLocation" IS 'Location header of a 201 response | header Location ของคำตอบ 201';
COMMENT ON COLUMN "IdempotencyKeys"."CreatedAt" IS 'When the key was first used (UTC) | เวลาที่ใช้ key ครั้งแรก (UTC)';
COMMENT ON COLUMN "IdempotencyKeys"."ExpiresAt" IS 'After this time the key is deleted and may be reused (UTC) | หลังเวลานี้ key จะถูกลบและนำกลับมาใช้ได้ (UTC)';
