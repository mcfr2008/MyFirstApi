using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;

namespace MyFirstApi.Services;

// Preventive maintenance: keeps monthly TrackingEvents partitions created ahead
// of time (see Scripts/025) so inserts never fall into the default partition, and
// warns when the default partition does collect rows. Runs at startup, then daily.
public class PartitionMaintenanceService : BackgroundService
{
    private const int MonthsAhead = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PartitionMaintenanceService> _logger;

    public PartitionMaintenanceService(IServiceScopeFactory scopeFactory, ILogger<PartitionMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var created = await db.Database
                .SqlQuery<int>($"SELECT create_tracking_event_partitions({MonthsAhead}) AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (created > 0)
            {
                _logger.LogInformation("Created {Count} TrackingEvents partition(s).", created);
            }

            // Planner estimate (updated by autovacuum/ANALYZE): cheap, no table scan.
            var defaultRows = await db.Database
                .SqlQuery<long>($"SELECT COALESCE((SELECT reltuples::bigint FROM pg_class WHERE relname = 'TrackingEvents_default'), 0) AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (defaultRows > 0)
            {
                _logger.LogWarning(
                    "About {Rows} tracking events are in the default partition (outside all monthly partitions). " +
                    "Check Scripts/maintenance/health_check.sql.", defaultRows);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never take the API down over maintenance; the next run retries.
            _logger.LogError(ex, "TrackingEvents partition maintenance failed.");
        }
    }
}
