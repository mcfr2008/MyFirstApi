-- Feature: Permission-based authorization
-- Schema for MyFirstApi.Models.Permission / RolePermission
-- (Authorization/PermissionAuthorizationHandler.cs reads these at request time).

-- Catalog of permission codes. Each code corresponds to an
-- [Authorize(Policy = "...")] attribute in the code (Authorization/Permissions.cs).
CREATE TABLE IF NOT EXISTS "Permissions" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(100) NOT NULL,
    "Description" VARCHAR(255),
    CONSTRAINT "IX_Permissions_Code" UNIQUE ("Code")
);

-- Maps a Role (the same free-text value stored on Users.Role) to the
-- permissions it grants. This is the table that actually controls access -
-- edit it directly to change what a role can do, without redeploying code.
CREATE TABLE IF NOT EXISTS "RolePermissions" (
    "Id" SERIAL PRIMARY KEY,
    "Role" VARCHAR(100) NOT NULL,
    "PermissionId" INT NOT NULL REFERENCES "Permissions"("Id") ON DELETE CASCADE,
    CONSTRAINT "IX_RolePermissions_Role_PermissionId" UNIQUE ("Role", "PermissionId")
);
