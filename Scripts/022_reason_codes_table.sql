-- Feature: Master data - reason codes for exception events
-- Schema for MyFirstApi.Models.ReasonCode / ReasonCodesController.

CREATE TABLE IF NOT EXISTS "ReasonCodes" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "NameTh" VARCHAR(255) NOT NULL,
    "NameEn" VARCHAR(255) NOT NULL,
    "Description" VARCHAR(2000),
    -- EventTypes.Code values this reason applies to; empty = any.
    "EventTypeCodes" TEXT[] NOT NULL DEFAULT '{}',
    "RequiresNote" BOOLEAN NOT NULL DEFAULT FALSE,
    "SortOrder" INT NOT NULL DEFAULT 0,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_ReasonCodes_Code" UNIQUE ("Code")
);

-- Event types that must be recorded with a reason. The UPDATE runs only when
-- the column is first added, so later edits via /api/EventTypes are kept.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'EventTypes' AND column_name = 'RequiresReason'
    ) THEN
        ALTER TABLE "EventTypes" ADD COLUMN "RequiresReason" BOOLEAN NOT NULL DEFAULT FALSE;
        UPDATE "EventTypes" SET "RequiresReason" = TRUE
        WHERE "Code" IN ('DELIVERY_FAILED', 'DAMAGED', 'LOST', 'RETURNED', 'CUSTOMS_HOLD');
    END IF;
END $$;
