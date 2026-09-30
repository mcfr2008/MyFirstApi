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
\ir 006_tracked_items_table.sql
\ir 007_item_categories_table.sql
\ir 008_locations_table.sql
\ir 009_parties_table.sql
\ir 010_event_types_table.sql
\ir 011_event_types_seed.sql
\ir 012_tracked_items_master_links.sql
\ir 013_locations_multimodal.sql
\ir 014_item_categories_dangerous_goods.sql
\ir 015_carriers_table.sql
\ir 016_vehicles_table.sql
\ir 017_containers_table.sql
\ir 018_tracked_items_international.sql
\ir 019_shipments_tables.sql
\ir 020_tracking_events_table.sql
\ir 021_event_types_multimodal_seed.sql
\ir 022_reason_codes_table.sql
\ir 023_reason_codes_seed.sql
\ir 024_tracking_events_void_reason.sql
\ir 025_tracking_events_partitioning.sql
\ir 026_search_indexes_and_storage.sql
\ir 027_foreign_key_indexes.sql
\ir 028_table_column_comments.sql
