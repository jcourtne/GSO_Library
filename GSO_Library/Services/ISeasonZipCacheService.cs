using GSO_Library.Models;

namespace GSO_Library.Services;

public interface ISeasonZipCacheService
{
    Task<(Stream stream, string zipFileName)> GetOrGenerateAsync(
        Season season, string zipKey, string? scorePartType, int? instrumentId);
    Task InvalidateForSeasonAsync(int seasonId);
    Task InvalidateForArrangementAsync(int arrangementId);
    string BuildZipKey(string? scorePartType, int? instrumentId);
}
