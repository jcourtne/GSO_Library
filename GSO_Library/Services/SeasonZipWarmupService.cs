using GSO_Library.Repositories;

namespace GSO_Library.Services;

/// <summary>
/// Pre-generates the "download all" share zip for a season so the first public visitor
/// gets a cache hit instead of waiting for the on-demand build.
/// </summary>
public interface ISeasonZipWarmupService
{
    Task WarmSeasonAsync(int seasonId, CancellationToken ct = default);
}

public class SeasonZipWarmupService(
    SeasonRepository seasonRepo,
    ISeasonZipCacheService zipCache,
    ILogger<SeasonZipWarmupService> logger) : ISeasonZipWarmupService
{
    public async Task WarmSeasonAsync(int seasonId, CancellationToken ct = default)
    {
        // Re-fetch: the controller's Season object predates the share-config upsert, so its
        // share_token / share_include_* flags would be stale.
        var season = await seasonRepo.GetSeasonByIdAsync(seasonId);
        if (season is null || string.IsNullOrEmpty(season.ShareToken))
            return; // season deleted or share revoked before we ran

        var zipKey = zipCache.BuildZipKey(null, null); // "all"
        await zipCache.EnsureGeneratedAsync(season, zipKey, null, null);
        logger.LogInformation("Pre-generated \"all\" share zip for season {SeasonId}", seasonId);
    }
}
