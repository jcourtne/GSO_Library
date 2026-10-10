using GSO_Library.Configuration;
using Microsoft.Extensions.Options;

namespace GSO_Library.Services;

/// <summary>
/// Periodically deletes cached season-share zips (storage object + DB row) whose
/// <c>created_at</c> is older than the configured retention window, plus any stored zip
/// left without a DB row. Expired zips regenerate transparently on the next public
/// download request.
/// </summary>
public class SeasonZipCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<SeasonZipCleanupService> logger,
    IOptions<SeasonZipCacheOptions> options) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var interval = TimeSpan.FromHours(Math.Max(1, opts.SweepIntervalHours));
        var maxAge = TimeSpan.FromDays(Math.Max(1, opts.RetentionDays));
        var orphanGrace = TimeSpan.FromMinutes(Math.Max(30, opts.OrphanGraceMinutes));

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval);
        do
        {
            await SweepAsync(maxAge, orphanGrace, stoppingToken);
        }
        while (await SafeWaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task SweepAsync(TimeSpan maxAge, TimeSpan orphanGrace, CancellationToken stoppingToken)
    {
        // Independent steps: a failure in one must not skip the other for a whole interval.
        await RunStepAsync("expired", zipCache => zipCache.PurgeExpiredAsync(maxAge, stoppingToken), stoppingToken);
        await RunStepAsync("orphaned", zipCache => zipCache.PurgeOrphanedAsync(orphanGrace, stoppingToken), stoppingToken);
    }

    private async Task RunStepAsync(string kind, Func<ISeasonZipCacheService, Task<int>> purge, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var zipCache = scope.ServiceProvider.GetRequiredService<ISeasonZipCacheService>();
            var deleted = await purge(zipCache);
            if (deleted > 0)
                logger.LogInformation("Season zip cleanup: purged {Count} {Kind} cached zip(s)", deleted, kind);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Season zip cleanup ({Kind}) failed; will retry on the next interval", kind);
        }
    }

    private static async Task<bool> SafeWaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
