-- Runs every script in this folder in order.
-- Usage: psql -h localhost -p 5432 -U postgres -d myfirstapi_db -f Scripts/run_all.sql
--
-- Uses \ir (include-relative) so this works regardless of the directory
-- psql was launched from - paths are resolved relative to this file.

\ir 001_products_table.sql
\ir 002_users_table.sql
\ir 003_users_seed_admin.sql
\ir 004_permissions_tables.sql
\ir 005_permissions_seed.sql
