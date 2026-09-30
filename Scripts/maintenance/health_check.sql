-- Database health report for preventive maintenance (read-only, safe to run anytime).
-- Usage: docker exec -i postgres-server psql -U postgres -d myfirstapi_db < Scripts/maintenance/health_check.sql
-- See Scripts/maintenance/README.md for what to do about each section.

\pset footer off

\echo '== 1. Largest tables (incl. indexes; partitioned tables summed over partitions) =='
WITH sizes AS (
    SELECT c.relname AS table_name,
           COALESCE(sum(pg_total_relation_size(p.relid)), pg_total_relation_size(c.oid)) AS total_bytes,
           COALESCE(sum(pg_indexes_size(p.relid)), pg_indexes_size(c.oid)) AS index_bytes,
           COALESCE(sum(pc.reltuples) FILTER (WHERE pc.reltuples > 0), c.reltuples)::bigint AS approx_rows
    FROM pg_class c
    LEFT JOIN LATERAL pg_partition_tree(c.oid) p ON c.relkind = 'p' AND p.isleaf
    LEFT JOIN pg_class pc ON pc.oid = p.relid
    WHERE c.relnamespace = 'public'::regnamespace AND c.relkind IN ('r', 'p') AND NOT c.relispartition
    GROUP BY c.oid, c.relname
)
SELECT table_name, pg_size_pretty(total_bytes) AS total_size, pg_size_pretty(index_bytes) AS index_size, approx_rows
FROM sizes
ORDER BY total_bytes DESC
LIMIT 10;

\echo '== 2. TrackingEvents partitions (need current month + 3 ahead; default should stay empty) =='
SELECT c.relname AS partition_name,
       pg_get_expr(c.relpartbound, c.oid) AS bounds,
       c.reltuples::bigint AS approx_rows,
       pg_size_pretty(pg_total_relation_size(c.oid)) AS size
FROM pg_inherits i
JOIN pg_class c ON c.oid = i.inhrelid
WHERE i.inhparent = '"TrackingEvents"'::regclass
ORDER BY c.relname DESC
LIMIT 8;

SELECT count(*) AS rows_in_default_partition FROM "TrackingEvents_default";

SELECT max(c.relname) AS furthest_partition,
       max(c.relname) >= format('TrackingEvents_%s', to_char(now() + interval '3 months', 'YYYY_MM')) AS three_months_ahead_ok
FROM pg_inherits i JOIN pg_class c ON c.oid = i.inhrelid
WHERE i.inhparent = '"TrackingEvents"'::regclass AND c.relname <> 'TrackingEvents_default';

\echo '== 3. Dead rows / vacuum status (high dead_pct = bloat; check autovacuum) =='
SELECT relname AS table_name, n_live_tup, n_dead_tup,
       round(100.0 * n_dead_tup / NULLIF(n_live_tup + n_dead_tup, 0), 1) AS dead_pct,
       last_autovacuum, last_autoanalyze
FROM pg_stat_user_tables
WHERE n_live_tup + n_dead_tup > 1000
ORDER BY n_dead_tup DESC
LIMIT 10;

\echo '== 4. Unused indexes since stats reset (candidates to drop; ignore unique/PK) =='
SELECT s.relname AS table_name, s.indexrelname AS index_name,
       pg_size_pretty(pg_relation_size(s.indexrelid)) AS size, s.idx_scan
FROM pg_stat_user_indexes s
JOIN pg_index i ON i.indexrelid = s.indexrelid
WHERE s.idx_scan = 0 AND NOT i.indisunique AND pg_relation_size(s.indexrelid) > 1024 * 1024
ORDER BY pg_relation_size(s.indexrelid) DESC
LIMIT 10;

\echo '== 5. Slowest queries (needs pg_stat_statements, see enable_pg_stat_statements.sql) =='
SELECT current_setting('shared_preload_libraries') LIKE '%pg_stat_statements%'
       AND EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_stat_statements') AS has_pss \gset
\if :has_pss
    SELECT round(mean_exec_time::numeric, 1) AS mean_ms, calls,
           round(total_exec_time::numeric) AS total_ms, left(regexp_replace(query, '\s+', ' ', 'g'), 120) AS query
    FROM pg_stat_statements
    WHERE dbid = (SELECT oid FROM pg_database WHERE datname = current_database())
    ORDER BY mean_exec_time DESC
    LIMIT 10;
\else
    \echo 'pg_stat_statements not enabled - skipped.'
\endif

\echo '== 6. Foreign keys without an index (deletes/updates of the referenced row scan this table) =='
SELECT c.conrelid::regclass AS table_name, a.attname AS fk_column, c.confrelid::regclass AS references_table
FROM pg_constraint c
JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
JOIN pg_class t ON t.oid = c.conrelid
WHERE c.contype = 'f' AND array_length(c.conkey, 1) = 1 AND NOT t.relispartition
  AND NOT EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = c.conrelid AND i.indkey[0] = c.conkey[1])
ORDER BY 1, 2;
