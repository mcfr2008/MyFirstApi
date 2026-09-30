-- Manually make sure TrackingEvents partitions exist for this month + next 3.
-- The API does this automatically at startup and daily (PartitionMaintenanceService);
-- run this if the API has been down for a long time or before a bulk import.
-- Usage: docker exec -i postgres-server psql -U postgres -d myfirstapi_db < Scripts/maintenance/create_partitions.sql

SELECT create_tracking_event_partitions(3) AS partitions_created;
