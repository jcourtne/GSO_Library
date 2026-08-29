using System.IO.Compression;
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSO_Library.Tests;

public class SeasonZipWarmupTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SeasonZipWarmupTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ───── Helpers ─────

    private async Task<(int seasonId, int arrangementId)> CreateSharedSeasonWithPdfAsync(bool includePdf = true, bool includeNotation = false)
    {
        var client = await GetLibrarianClientAsync();

        var ensembleResp = await client.PostAsJsonAsync("/api/ensembles", new { Name = $"Ens_{Guid.NewGuid():N}" });
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

        var arrResp = await client.PostAsJsonAsync("/api/arrangements", new { Name = $"Arr_{Guid.NewGuid():N}" });
        arrResp.EnsureSuccessStatusCode();
        var arrangementId = (await arrResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetInt32();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("pdf content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "score.pdf");
        (await client.PostAsync($"/api/arrangements/{arrangementId}/files", content)).EnsureSuccessStatusCode();

        (await client.PostAsync($"/api/seasons/{seasonId}/arrangements/{arrangementId}", null)).EnsureSuccessStatusCode();

        await ConfigureShareAsync(seasonId, includePdf, includeNotation);

        return (seasonId, arrangementId);
    }

    private async Task ConfigureShareAsync(int seasonId, bool includePdf, bool includeNotation)
    {
        var client = await GetLibrarianClientAsync();
        var resp = await client.PostAsJsonAsync($"/api/seasons/{seasonId}/share", new
        {
            IncludePdf = includePdf,
            IncludeNotation = includeNotation,
            IncludePlayback = false,
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task WarmAsync(int seasonId)
    {
        using var scope = Factory.Services.CreateScope();
        var warmup = scope.ServiceProvider.GetRequiredService<ISeasonZipWarmupService>();
        await warmup.WarmSeasonAsync(seasonId);
    }

    private async Task<bool> RowExistsAsync(int seasonId, string zipKey)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<SeasonShareZipRepository>();
        return await repo.GetAsync(seasonId, zipKey) != null;
    }

    private async Task<List<string>?> AllZipEntryNamesAsync(int seasonId)
    {
        using var scope = Factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        try
        {
            await using var stream = await storage.GetFileAsync($"shares/{seasonId}", "all.zip");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            return archive.Entries.Select(e => e.FullName).ToList();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    // ───── Tests ─────

    [Fact]
    public async Task ConfigureShare_EnqueuesWarmupForSeason()
    {
        var (seasonId, _) = await CreateSharedSeasonWithPdfAsync();

        // The background consumer is stripped in tests (IHostedService removed), so queued
        // requests stay readable.
        var queue = Factory.Services.GetRequiredService<ISeasonZipWarmupQueue>();
        var seen = new List<int>();
        while (queue.Reader.TryRead(out var req))
            seen.Add(req.SeasonId);

        Assert.Contains(seasonId, seen);
    }

    [Fact]
    public async Task WarmSeasonAsync_BuildsAllZip_ContainingSharedFiles()
    {
        var (seasonId, _) = await CreateSharedSeasonWithPdfAsync();

        await WarmAsync(seasonId);

        Assert.True(await RowExistsAsync(seasonId, "all"));
        var entries = await AllZipEntryNamesAsync(seasonId);
        Assert.NotNull(entries);
        Assert.Contains(entries!, e => e.EndsWith("score.pdf"));
    }

    [Fact]
    public async Task WarmSeasonAsync_HonoursUpdatedShareFlags()
    {
        var (seasonId, _) = await CreateSharedSeasonWithPdfAsync(includePdf: true);
        await WarmAsync(seasonId);
        Assert.Contains((await AllZipEntryNamesAsync(seasonId))!, e => e.EndsWith("score.pdf"));

        // Re-configure: drop PDFs. ConfigureShare invalidates the cached zip; the warm-up
        // must re-fetch the season rather than reuse the stale flags.
        await ConfigureShareAsync(seasonId, includePdf: false, includeNotation: true);
        await WarmAsync(seasonId);

        var entries = await AllZipEntryNamesAsync(seasonId);
        Assert.NotNull(entries);
        Assert.DoesNotContain(entries!, e => e.EndsWith("score.pdf"));
    }

    [Fact]
    public async Task WarmSeasonAsync_NoOpAfterShareRevoked()
    {
        var (seasonId, _) = await CreateSharedSeasonWithPdfAsync();

        var client = await GetLibrarianClientAsync();
        var revoke = await client.DeleteAsync($"/api/seasons/{seasonId}/share");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        await WarmAsync(seasonId);

        Assert.False(await RowExistsAsync(seasonId, "all"));
    }

    [Fact]
    public void Queue_DedupesPendingSeason_UntilMarkedStarted()
    {
        var queue = new SeasonZipWarmupQueue(NullLogger<SeasonZipWarmupQueue>.Instance);

        queue.Enqueue(new SeasonZipWarmupRequest(42));
        queue.Enqueue(new SeasonZipWarmupRequest(42));

        Assert.True(queue.Reader.TryRead(out var first));
        Assert.Equal(42, first!.SeasonId);
        Assert.False(queue.Reader.TryRead(out _));

        queue.MarkStarted(42);
        queue.Enqueue(new SeasonZipWarmupRequest(42));
        Assert.True(queue.Reader.TryRead(out _));
    }
}
