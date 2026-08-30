namespace GSO_Library.Services;

/// <summary>
/// Single consumer of <see cref="ISeasonZipWarmupQueue"/>. Builds each queued season's
/// "download all" share zip in its own DI scope. Failures are logged and the loop
/// continues; the public download path still generates lazily as a fallback.
/// </summary>
public class SeasonZipWarmupBackgroundService(
    IServiceScopeFactory scopeFactory,
    ISeasonZipWarmupQueue queue,
    ILogger<SeasonZipWarmupBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var req in queue.Reader.ReadAllAsync(stoppingToken))
            {
                queue.MarkStarted(req.SeasonId);
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<ISeasonZipWarmupService>();
                    await svc.WarmSeasonAsync(req.SeasonId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Season zip warm-up failed for season {SeasonId}", req.SeasonId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
