-- Feature: Authentication
-- Schema for MyFirstApi.Models.User / AuthController.

CREATE TABLE IF NOT EXISTS "Users" (
    "Id" SERIAL PRIMARY KEY,
    "Username" VARCHAR(255) NOT NULL,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "Role" VARCHAR(100) NOT NULL DEFAULT 'User',
    CONSTRAINT "IX_Users_Username" UNIQUE ("Username")
);
