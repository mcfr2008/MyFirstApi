-- Enables query statistics (slowest / most frequent queries), used by health_check.sql.
-- Needs a PostgreSQL restart between step 1 and step 2.
--
-- 1) docker exec -i postgres-server psql -U postgres -d myfirstapi_db < Scripts/maintenance/enable_pg_stat_statements.sql
-- 2) docker restart postgres-server
-- 3) run this file again (it then creates the extension)

SELECT current_setting('shared_preload_libraries') LIKE '%pg_stat_statements%' AS preloaded \gset

\if :preloaded
    CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
    \echo 'pg_stat_statements is enabled.'
\else
    ALTER SYSTEM SET shared_preload_libraries = 'pg_stat_statements';
    \echo 'Configured. Restart PostgreSQL (docker restart postgres-server), then run this file again.'
\endif
