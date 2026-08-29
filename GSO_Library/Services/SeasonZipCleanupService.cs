using GSO_Library.Configuration;
using Microsoft.Extensions.Options;

namespace GSO_Library.Services;

/// <summary>
/// Periodically deletes cached season-share zips (storage object + DB row) whose
/// <c>created_at</c> is older than the configured retention window. Expired zips
/// regenerate transparently on the next public download request.
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
            await SweepAsync(maxAge, stoppingToken);
        }
        while (await SafeWaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task SweepAsync(TimeSpan maxAge, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var zipCache = scope.ServiceProvider.GetRequiredService<ISeasonZipCacheService>();
            var deleted = await zipCache.PurgeExpiredAsync(maxAge, stoppingToken);
            if (deleted > 0)
                logger.LogInformation("Season zip cleanup: purged {Count} expired cached zip(s)", deleted);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Season zip cleanup sweep failed; will retry on the next interval");
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
