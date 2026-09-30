-- Feature: Master data - tracking event types
-- Schema for MyFirstApi.Models.EventType / EventTypesController.

CREATE TABLE IF NOT EXISTS "EventTypes" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL,
    "NameTh" VARCHAR(255) NOT NULL,
    "NameEn" VARCHAR(255) NOT NULL,
    "Description" VARCHAR(2000),
    -- ItemStatus enum name the item moves to when this event is recorded;
    -- NULL = informational event, status unchanged.
    "ResultingStatus" VARCHAR(30),
    "IsTerminal" BOOLEAN NOT NULL DEFAULT FALSE,
    "SortOrder" INT NOT NULL DEFAULT 0,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_EventTypes_Code" UNIQUE ("Code")
);
