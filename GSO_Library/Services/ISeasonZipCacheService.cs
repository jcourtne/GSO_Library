using GSO_Library.Models;

namespace GSO_Library.Services;

public interface ISeasonZipCacheService
{
    Task<(Stream stream, string zipFileName)> GetOrGenerateAsync(
        Season season, string zipKey, string? scorePartType, int? instrumentId, int? familyId = null);
    Task EnsureGeneratedAsync(
        Season season, string zipKey, string? scorePartType, int? instrumentId, int? familyId = null);
    Task InvalidateForSeasonAsync(int seasonId);
    Task InvalidateForArrangementAsync(int arrangementId);
    Task<int> PurgeExpiredAsync(TimeSpan maxAge, CancellationToken ct = default);
    string BuildZipKey(string? scorePartType, int? instrumentId, int? familyId = null);
}
