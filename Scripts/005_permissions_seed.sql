-- Feature: Permission-based authorization (seed data)
-- Add a new permission by inserting a row here (plus a matching
-- [Authorize(Policy = "...")] attribute in code) - no other code change needed.

INSERT INTO "Permissions" ("Code", "Description") VALUES
    ('Products.Read', 'View products'),
    ('Products.Create', 'Create products'),
    ('Products.Update', 'Update products'),
    ('Products.Delete', 'Delete products'),
    ('Auth.Register', 'Create user accounts and assign roles')
ON CONFLICT ("Code") DO NOTHING;

-- Admin: full access to every known permission.
INSERT INTO "RolePermissions" ("Role", "PermissionId")
SELECT 'Admin', "Id" FROM "Permissions"
ON CONFLICT ("Role", "PermissionId") DO NOTHING;

-- User: read-only access to products.
INSERT INTO "RolePermissions" ("Role", "PermissionId")
SELECT 'User', "Id" FROM "Permissions" WHERE "Code" = 'Products.Read'
ON CONFLICT ("Role", "PermissionId") DO NOTHING;
