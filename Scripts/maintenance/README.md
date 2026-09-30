# Database preventive maintenance

Keeps Thing-Tag fast as tracking history grows. The design:

- **Current state lives on the item.** `TrackedItems.Status`, `CurrentLocationId` and `LastEventAt` answer "where is it now?" without reading history, so everyday screens don't slow down as history grows.
- **`TrackingEvents` is partitioned by month** (`Scripts/025`). Date-range queries read only the months they need, each month's indexes stay small, and old months can be detached in one step.
- **Event lists use cursor paging** (`?cursor=`). A page costs the same whether it's page 1 or page 10,000, and no count runs unless `includeTotalCount=true`.
- **Substring searches use trigram indexes** (`Scripts/026`).
- **Event types and reason codes are cached** in memory (`Services/MasterDataCache.cs`).

## Automatic

| What | Where |
|---|---|
| Create partitions for this month + 3 ahead, at startup and every 24 h; warn if the default partition gets rows | `Services/PartitionMaintenanceService.cs` (logs) |
| Autovacuum tuned for insert-heavy partitions and the frequently-updated `TrackedItems` | `Scripts/025`, `Scripts/026` |

## Schedule

Commands assume the Docker container `postgres-server`:
`docker exec -i postgres-server psql -U postgres -d myfirstapi_db < <file>`

| When | Task | How |
|---|---|---|
| Daily | Check that the backup succeeded; check API logs for `PartitionMaintenanceService` warnings/errors | backup tool, logs |
| Weekly | Run the health check; watch table growth, dead rows (`dead_pct` > 20% on a big table means autovacuum isn't keeping up), slow queries, and foreign keys without an index (section 6 should be empty) | `health_check.sql` |
| Monthly | Confirm future partitions exist (`three_months_ahead_ok` = t) and the default partition is empty; `REINDEX INDEX CONCURRENTLY` any index that has grown far beyond its table; **test-restore a backup** | `health_check.sql`, `create_partitions.sql` |
| Quarterly | Load test on a copy: `Scripts/dev/load_test_seed.sql` + `python3 Scripts/dev/benchmark.py`; compare with the targets below; review data retention | `Scripts/dev/` |

## Targets

Alert or investigate when these are exceeded:

| Operation | Target (p95) |
|---|---|
| Item by tag / scan | < 200 ms |
| Item timeline | < 300 ms |
| Any single query | < 500 ms |

Measured on a laptop with 3M events, 200k items and 50k shipments:

| Case | Before | After |
|---|---|---|
| Item by tag code | 3.5 ms | 2.3 ms |
| Item timeline | 7.0 ms | 4.0 ms |
| Latest events, first page | 236 ms | 4.7 ms |
| Events at one location | 37 ms | 3.9 ms |
| Events in a 1-week range | 20 ms | 2.7 ms |
| Item substring search | 150 ms | 3.8 ms |
| Shipment search by B/L | 142 ms | 24 ms |
| Scan 100 tags (write) | 25 ms | 25 ms |
| Deep page (row 50,000), SQL | 65 ms (OFFSET) | 5 ms (cursor) |

## If something goes wrong

- **Rows in `TrackingEvents_default`.** These are events outside every monthly partition, usually heavily back-dated ones. That's fine in small numbers. To give an old month its own partition, move the rows first: create a table for the month, copy the rows, delete them from the default partition, and attach the table.
- **Missing future partition.** Inserts still succeed (they go to the default partition), but run `create_partitions.sql` and check why the API job didn't run.
- **Disk space not freed after a big delete.** `DELETE` leaves dead space that plain `VACUUM` reuses but doesn't give back. `VACUUM FULL <table>` gives it back but locks the table, so only run it in a maintenance window. For history, drop or detach whole partitions instead of deleting rows.
- **Slow delete or update of a referenced row.** A foreign key without an index makes each check scan the referencing table (health check section 6). Add the index; see `Scripts/027`.
- **Slow query.** Enable `pg_stat_statements` (`enable_pg_stat_statements.sql`), find the query in `health_check.sql` section 5, and run `EXPLAIN (ANALYZE, BUFFERS)` on it.

## Later (phase 3)

These aren't needed yet:
- Detach old partitions to an archive schema or export them to object storage, following the legal and contractual retention period.
- Nightly rollup tables for dashboards.
- A read replica for reporting.
