using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;

namespace MyFirstApi.Services;

// Deletes expired idempotency keys (Idempotency/IdempotencyFilter.cs) so the table
// only holds the retention window. Runs at startup, then hourly.
public class IdempotencyKeyCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyKeyCleanupService> _logger;

    public IdempotencyKeyCleanupService(IServiceScopeFactory scopeFactory, ILogger<IdempotencyKeyCleanupService> logger)
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

            var now = DateTime.UtcNow;
            var deleted = await db.IdempotencyKeys
                .Where(k => k.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {Count} expired idempotency key(s).", deleted);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Idempotency key cleanup failed.");
        }
    }
}
