using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GSO_Library.Tests;

public class SeasonZipCleanupTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SeasonZipCleanupTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<int> CreateSharedSeasonAsync()
    {
        var client = await GetLibrarianClientAsync();

        var adminClient = await GetAdminClientAsync();
        var ensembleResp = await adminClient.PostAsJsonAsync("/api/ensembles", new { Name = $"Ens_{Guid.NewGuid():N}" });
        ensembleResp.EnsureSuccessStatusCode();
        var ensembleId = (await ensembleResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetInt32();

        var seasonResp = await client.PostAsJsonAsync("/api/seasons", new
        {
            Name = $"Season_{Guid.NewGuid():N}",
            EnsembleId = ensembleId,
            StartDate = "2025-09-01",
            EndDate = "2025-12-31",
        });
        seasonResp.EnsureSuccessStatusCode();
        var seasonId = (await seasonResp.Content.ReadFromJsonAsync<Season>(JsonOpts))!.Id;

        var shareResp = await client.PostAsJsonAsync($"/api/seasons/{seasonId}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
        });
        shareResp.EnsureSuccessStatusCode();

        return seasonId;
    }

    private async Task SeedZipAsync(int seasonId, string zipKey, DateTime createdAtUtc)
    {
        using var scope = Factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var folderPath = $"shares/{seasonId}";
        var storedFileName = $"{zipKey}.zip";

        await storage.SaveFileAsync(folderPath, storedFileName,
            new MemoryStream(Encoding.UTF8.GetBytes("fake zip content")));

        using var connection = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>().CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO season_share_zips (season_id, zip_key, folder_path, stored_file_name, created_at)
              VALUES (@SeasonId, @ZipKey, @FolderPath, @StoredFileName, @CreatedAt)",
            new { SeasonId = seasonId, ZipKey = zipKey, FolderPath = folderPath, StoredFileName = storedFileName, CreatedAt = createdAtUtc });
    }

    private async Task<bool> FileExistsAsync(int seasonId, string zipKey)
    {
        using var scope = Factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        try
        {
            await using var _ = await storage.GetFileAsync($"shares/{seasonId}", $"{zipKey}.zip");
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private async Task<bool> RowExistsAsync(int seasonId, string zipKey)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<SeasonShareZipRepository>();
        return await repo.GetAsync(seasonId, zipKey) != null;
    }

    [Fact]
    public async Task PurgeExpiredAsync_DeletesZipsOlderThanMaxAge_KeepsFreshOnes()
    {
        var seasonId = await CreateSharedSeasonAsync();
        await SeedZipAsync(seasonId, "stale", DateTime.UtcNow.AddDays(-40));
        await SeedZipAsync(seasonId, "fresh", DateTime.UtcNow);

        int deleted;
        using (var scope = Factory.Services.CreateScope())
        {
            var zipCache = scope.ServiceProvider.GetRequiredService<ISeasonZipCacheService>();
            deleted = await zipCache.PurgeExpiredAsync(TimeSpan.FromDays(30));
        }

        Assert.Equal(1, deleted);
        Assert.False(await RowExistsAsync(seasonId, "stale"));
        Assert.False(await FileExistsAsync(seasonId, "stale"));
        Assert.True(await RowExistsAsync(seasonId, "fresh"));
        Assert.True(await FileExistsAsync(seasonId, "fresh"));
    }

    [Fact]
    public async Task RevokeShare_RemovesCachedZipRowAndFile()
    {
        var client = await GetLibrarianClientAsync();
        var seasonId = await CreateSharedSeasonAsync();
        await SeedZipAsync(seasonId, "all", DateTime.UtcNow);

        var revoke = await client.DeleteAsync($"/api/seasons/{seasonId}/share");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        Assert.False(await RowExistsAsync(seasonId, "all"));
        Assert.False(await FileExistsAsync(seasonId, "all"));
    }
}
