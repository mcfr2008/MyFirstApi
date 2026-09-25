-- Feature: Authentication (seed data)
-- Bootstrap admin account, since POST /api/Auth/register requires the
-- Auth.Register permission and there would otherwise be no account to grant it.
--
-- Username: admin | Password: ChangeMe123!
-- CHANGE THIS PASSWORD IMMEDIATELY after first login (there is no self-service
-- change-password endpoint yet; update the PasswordHash column directly, or add one).

INSERT INTO "Users" ("Username", "PasswordHash", "Role")
VALUES ('admin', '$2y$10$eqEZAnDoFKaNa0B3MMSrP.HPQjlwUAqKYzZ6sKMW7B01yuYEKnK8S', 'Admin')
ON CONFLICT ("Username") DO NOTHING;
