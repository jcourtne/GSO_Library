using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class SeasonsControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SeasonsControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ───── Helpers ─────

    private async Task<int> CreateArrangementAsync(HttpClient client, string name = "Test Arrangement")
    {
        var response = await client.PostAsJsonAsync("/api/arrangements", new { Name = name });
        response.EnsureSuccessStatusCode();
        var arr = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return arr.GetProperty("id").GetInt32();
    }

    private async Task<string> ConfigureShareAsync(HttpClient client, int seasonId, bool includePdf = true)
    {
        var response = await client.PostAsJsonAsync($"/api/seasons/{seasonId}/share", new
        {
            IncludePdf = includePdf,
            IncludeNotation = false,
            IncludePlayback = false,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("token").GetString()!;
    }

    private async Task<Ensemble> CreateEnsembleAsync(HttpClient client, string name = "Test Ensemble")
    {
        var response = await client.PostAsJsonAsync("/api/ensembles", new { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Ensemble>(JsonOpts))!;
    }

    private async Task<Season> CreateSeasonAsync(HttpClient client, int ensembleId, string name = "Test Season")
    {
        var response = await client.PostAsJsonAsync("/api/seasons", new
        {
            Name = name,
            EnsembleId = ensembleId,
            StartDate = "2025-09-01",
            EndDate = "2025-12-31",
            Notes = "Fall season"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Season>(JsonOpts))!;
    }

    // ───── Green cases ─────

    [Fact]
    public async Task CreateSeason_AsLibrarian_Returns201()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_Create");

        var response = await client.PostAsJsonAsync("/api/seasons", new
        {
            Name = "Fall 2025",
            EnsembleId = ensemble.Id,
            StartDate = "2025-09-01",
            EndDate = "2025-12-15"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var season = await response.Content.ReadFromJsonAsync<Season>(JsonOpts);
        Assert.Equal("Fall 2025", season!.Name);
        Assert.True(season.Id > 0);
        Assert.Equal(ensemble.Id, season.EnsembleId);
    }

    [Fact]
    public async Task GetAllSeasons_Authenticated_Returns200()
    {
        var editor = await GetEditorClientAsync();
        var ensemble = await CreateEnsembleAsync(editor, "Ens_GetAll");
        await CreateSeasonAsync(editor, ensemble.Id, "Season_GetAll");

        var user = await GetUserClientAsync();
        var response = await user.GetAsync("/api/seasons");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Season>>(JsonOpts);
        Assert.NotNull(result);
        Assert.True(result!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetSeasonById_Exists_Returns200WithLinkedData()
    {
        var editor = await GetEditorClientAsync();
        var ensemble = await CreateEnsembleAsync(editor, "Ens_GetById");
        var season = await CreateSeasonAsync(editor, ensemble.Id, "Season_GetById");

        var user = await GetUserClientAsync();
        var response = await user.GetAsync($"/api/seasons/{season.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<Season>(JsonOpts);
        Assert.Equal("Season_GetById", fetched!.Name);
        Assert.NotNull(fetched.Ensemble);
        Assert.Equal(ensemble.Id, fetched.Ensemble!.Id);
    }

    [Fact]
    public async Task UpdateSeason_AsLibrarian_Returns200()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_Update");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Old Season");

        var response = await client.PutAsJsonAsync($"/api/seasons/{season.Id}", new
        {
            Name = "Updated Season",
            EnsembleId = ensemble.Id,
            Notes = "Updated notes"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Season>(JsonOpts);
        Assert.Equal("Updated Season", updated!.Name);
    }

    [Fact]
    public async Task DeleteSeason_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_Delete");
        var season = await CreateSeasonAsync(client, ensemble.Id, "ToDelete Season");

        var response = await client.DeleteAsync($"/api/seasons/{season.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync($"/api/seasons/{season.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Sorting ─────

    [Fact]
    public async Task GetAllSeasons_SortByNameDesc_ReturnsSorted()
    {
        var client = await GetEditorClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_Sort");
        await CreateSeasonAsync(client, ensemble.Id, "AAA_SeasonSort");
        await CreateSeasonAsync(client, ensemble.Id, "ZZZ_SeasonSort");

        var response = await client.GetAsync("/api/seasons?sortBy=name&sortDirection=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Season>>(JsonOpts);
        var names = result!.Items.Select(s => s.Name).ToList();
        Assert.Equal(names.OrderByDescending(n => n).ToList(), names);
    }

    // ───── Filtering ─────

    [Fact]
    public async Task GetAllSeasons_FilterByEnsembleId_ReturnsOnlyMatching()
    {
        var client = await GetEditorClientAsync();
        var ensA = await CreateEnsembleAsync(client, "Ens_FilterA");
        var ensB = await CreateEnsembleAsync(client, "Ens_FilterB");
        await CreateSeasonAsync(client, ensA.Id, "Season_FilterA");
        await CreateSeasonAsync(client, ensB.Id, "Season_FilterB");

        var response = await client.GetAsync($"/api/seasons?ensembleIds={ensA.Id}");
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Season>>(JsonOpts);

        Assert.All(result!.Items, s => Assert.Equal(ensA.Id, s.EnsembleId));
    }

    // ───── Audit fields ─────

    [Fact]
    public async Task CreateSeason_SetsAuditFields()
    {
        var client = await GetEditorClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_Audit");

        var response = await client.PostAsJsonAsync("/api/seasons", new
        {
            Name = "AuditSeason",
            EnsembleId = ensemble.Id
        });
        response.EnsureSuccessStatusCode();
        var season = await response.Content.ReadFromJsonAsync<Season>(JsonOpts);

        Assert.NotEqual(default, season!.CreatedAt);
        Assert.NotEqual(default, season.UpdatedAt);
        Assert.Equal("testeditor", season.CreatedBy);
    }

    [Fact]
    public async Task CreateSeason_LogsAuditEvent()
    {
        var client = await GetEditorClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_AuditLog");
        var sinceId = await GetMaxAuditEventIdAsync();

        var response = await client.PostAsJsonAsync("/api/seasons", new
        {
            Name = "AuditLogSeason",
            EnsembleId = ensemble.Id
        });
        response.EnsureSuccessStatusCode();
        var season = await response.Content.ReadFromJsonAsync<Season>(JsonOpts);

        var events = await GetAuditEventsSinceAsync(sinceId, "SeasonCreate");
        Assert.Single(events);
        Assert.Contains($"seasonId: {season!.Id}", events[0].Detail);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetSeasonById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();
        var response = await client.GetAsync("/api/seasons/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSeason_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_UpdateNotFound");
        var response = await client.PutAsJsonAsync("/api/seasons/99999", new { Name = "Nope", EnsembleId = ensemble.Id });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSeason_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var response = await client.DeleteAsync("/api/seasons/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeason_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/seasons", new { Name = "Nope", EnsembleId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeason_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();
        var response = await client.PostAsJsonAsync("/api/seasons", new { Name = "Nope", EnsembleId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ───── Arrangement linking ─────

    [Fact]
    public async Task AddArrangement_AsLibrarian_Returns204()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_ArrLink");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_ArrLink");

        // Create an arrangement to link
        var series = await client.PostAsJsonAsync("/api/series", new { Name = "Series_ArrLink" });
        var seriesObj = await series.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var game = await client.PostAsJsonAsync("/api/games", new { Name = "Game_ArrLink", SeriesId = seriesObj.GetProperty("id").GetInt32() });
        var gameObj = await game.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var arr = await client.PostAsJsonAsync("/api/arrangements", new
        {
            Name = "Arr_ArrLink",
            GameIds = new[] { gameObj.GetProperty("id").GetInt32() }
        });
        var arrObj = await arr.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var arrangementId = arrObj.GetProperty("id").GetInt32();

        var response = await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arrangementId}", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify arrangement appears in detail
        var detail = await client.GetAsync($"/api/seasons/{season.Id}");
        var fetched = await detail.Content.ReadFromJsonAsync<Season>(JsonOpts);
        Assert.Contains(fetched!.Arrangements, a => a.Id == arrangementId);
    }

    [Fact]
    public async Task RemoveArrangement_AsLibrarian_Returns204()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_ArrUnlink");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_ArrUnlink");

        var series = await client.PostAsJsonAsync("/api/series", new { Name = "Series_ArrUnlink" });
        var seriesObj = await series.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var game = await client.PostAsJsonAsync("/api/games", new { Name = "Game_ArrUnlink", SeriesId = seriesObj.GetProperty("id").GetInt32() });
        var gameObj = await game.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var arr = await client.PostAsJsonAsync("/api/arrangements", new
        {
            Name = "Arr_ArrUnlink",
            GameIds = new[] { gameObj.GetProperty("id").GetInt32() }
        });
        var arrObj = await arr.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var arrangementId = arrObj.GetProperty("id").GetInt32();

        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arrangementId}", null);

        var response = await client.DeleteAsync($"/api/seasons/{season.Id}/arrangements/{arrangementId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await client.GetAsync($"/api/seasons/{season.Id}");
        var fetched = await detail.Content.ReadFromJsonAsync<Season>(JsonOpts);
        Assert.DoesNotContain(fetched!.Arrangements, a => a.Id == arrangementId);
    }

    [Fact]
    public async Task AddArrangement_SeasonNotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var response = await client.PostAsync("/api/seasons/99999/arrangements/1", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ───── Performance linking ─────

    [Fact]
    public async Task AddPerformance_AsLibrarian_Returns204()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_PerfLink");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_PerfLink");

        var perf = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Perf_PerfLink",
            Link = "https://example.com/perflink"
        });
        var perfObj = await perf.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var performanceId = perfObj.GetProperty("id").GetInt32();

        var response = await client.PostAsync($"/api/seasons/{season.Id}/performances/{performanceId}", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await client.GetAsync($"/api/seasons/{season.Id}");
        var fetched = await detail.Content.ReadFromJsonAsync<Season>(JsonOpts);
        Assert.Contains(fetched!.Performances, p => p.Id == performanceId);
    }

    [Fact]
    public async Task RemovePerformance_AsLibrarian_Returns204()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_PerfUnlink");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_PerfUnlink");

        var perf = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Perf_PerfUnlink",
            Link = "https://example.com/perfunlink"
        });
        var perfObj = await perf.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var performanceId = perfObj.GetProperty("id").GetInt32();

        await client.PostAsync($"/api/seasons/{season.Id}/performances/{performanceId}", null);

        var response = await client.DeleteAsync($"/api/seasons/{season.Id}/performances/{performanceId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await client.GetAsync($"/api/seasons/{season.Id}");
        var fetched = await detail.Content.ReadFromJsonAsync<Season>(JsonOpts);
        Assert.DoesNotContain(fetched!.Performances, p => p.Id == performanceId);
    }

    [Fact]
    public async Task AddPerformance_SeasonNotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var response = await client.PostAsync("/api/seasons/99999/performances/1", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LinkingEndpoints_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();
        var response = await client.PostAsync("/api/seasons/1/arrangements/1", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ───── Share link ─────

    [Fact]
    public async Task ConfigureShare_AsLibrarian_ReturnsToken()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_Share");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_Share");

        var response = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("token").GetString();
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task GetSeason_AfterConfiguringShareWithPassword_ReportsHasSharePassword()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_SharePwFlag");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_SharePwFlag");

        // No share configured yet → no password
        var before = await (await client.GetAsync($"/api/seasons/{season.Id}"))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.False(before.GetProperty("hasSharePassword").GetBoolean());

        // Configure share with a password
        var shareResponse = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
            Password = "secret123",
        });
        shareResponse.EnsureSuccessStatusCode();

        var withPw = await (await client.GetAsync($"/api/seasons/{season.Id}"))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(withPw.GetProperty("hasSharePassword").GetBoolean());

        // Clear the password
        var clearResponse = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
            ClearPassword = true,
        });
        clearResponse.EnsureSuccessStatusCode();

        var cleared = await (await client.GetAsync($"/api/seasons/{season.Id}"))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.False(cleared.GetProperty("hasSharePassword").GetBoolean());
    }

    [Fact]
    public async Task GetPublicSeason_WithValidToken_Returns200()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_PubGet");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_PubGet");

        var shareResponse = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
        });
        shareResponse.EnsureSuccessStatusCode();
        var body = await shareResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("token").GetString()!;

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("Season_PubGet", dto.GetProperty("name").GetString());
        Assert.False(dto.GetProperty("requiresPassword").GetBoolean());
    }

    [Fact]
    public async Task GetPublicSeason_InvalidToken_Returns404()
    {
        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync("/api/public/seasons/nonexistenttoken123");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPublicSeason_WithPassword_RequiresPassword()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_PubPw");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_PubPw");

        var shareResponse = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
            Password = "secret123",
        });
        shareResponse.EnsureSuccessStatusCode();
        var body = await shareResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("token").GetString()!;

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(dto.GetProperty("requiresPassword").GetBoolean());

        var withPw = GetUnauthenticatedClient();
        withPw.DefaultRequestHeaders.Add("X-Share-Password", "secret123");
        var authResponse = await withPw.GetAsync($"/api/public/seasons/{token}");
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
        var authed = await authResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.False(authed.GetProperty("requiresPassword").GetBoolean());
    }

    [Fact]
    public async Task RevokeShare_AsLibrarian_Returns204AndInvalidatesToken()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_Revoke");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_Revoke");

        var shareResponse = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
        });
        shareResponse.EnsureSuccessStatusCode();
        var body = await shareResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("token").GetString()!;

        var revokeResponse = await client.DeleteAsync($"/api/seasons/{season.Id}/share");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfigureShare_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();
        var response = await client.PostAsJsonAsync("/api/seasons/1/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ───── Public download ─────

    [Fact]
    public async Task Download_ValidToken_ReturnsZip()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLBasic");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLBasic");
        var token = await ConfigureShareAsync(client, season.Id);

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Download_WithPdfFile_ZipContainsFile()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLFile");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLFile");

        var arrangementId = await CreateArrangementAsync(client, "Arr_DLFile");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("pdf content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "score.pdf");
        var uploadResponse = await client.PostAsync($"/api/arrangements/{arrangementId}/files", content);
        uploadResponse.EnsureSuccessStatusCode();

        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arrangementId}", null);

        var token = await ConfigureShareAsync(client, season.Id);

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal(0x50, bytes[0]); // 'P' — zip PK signature
        Assert.Equal(0x4B, bytes[1]); // 'K'
    }

    [Fact]
    public async Task Download_FileTaggedWithInstrumentAndGenericSection_IncludedInBoth()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLDual");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLDual");

        var arrangementId = await CreateArrangementAsync(client, "Arr_DLDual");

        var instrumentResponse = await client.PostAsJsonAsync("/api/instruments", new { Name = "DLDual_Trumpet" });
        instrumentResponse.EnsureSuccessStatusCode();
        var instrument = await instrumentResponse.Content.ReadFromJsonAsync<Instrument>(JsonOpts);
        var instrumentId = instrument!.Id;

        var addInstrumentResponse = await client.PostAsync(
            $"/api/arrangements/{arrangementId}/instruments/{instrumentId}", null);
        addInstrumentResponse.EnsureSuccessStatusCode();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("pdf content"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "trumpet_and_voice.pdf");
        var uploadResponse = await client.PostAsync($"/api/arrangements/{arrangementId}/files", content);
        uploadResponse.EnsureSuccessStatusCode();
        var file = await uploadResponse.Content.ReadFromJsonAsync<ArrangementFile>(JsonOpts);

        // Dual-tag: filed under the generic "Voice (Generic)" section AND assigned to the Trumpet instrument.
        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/arrangements/{arrangementId}/files/{file!.Id}",
            new { ScorePartType = "voice_part", InstrumentIds = new[] { instrumentId } });
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arrangementId}", null);

        var token = await ConfigureShareAsync(client, season.Id);
        var anon = GetUnauthenticatedClient();

        var publicResponse = await anon.GetAsync($"/api/public/seasons/{token}");
        publicResponse.EnsureSuccessStatusCode();
        var publicDto = await publicResponse.Content.ReadFromJsonAsync<SeasonPublicDto>(JsonOpts);
        Assert.Contains(publicDto!.DownloadSections, s => s.InstrumentId == instrumentId && s.FileCount == 1);
        Assert.Contains(publicDto.DownloadSections, s => s.ScorePartType == "voice_part" && s.FileCount == 1);

        var instrumentZipResponse = await anon.GetAsync($"/api/public/seasons/{token}/download?instrumentId={instrumentId}");
        Assert.Equal(HttpStatusCode.OK, instrumentZipResponse.StatusCode);
        using (var archive = new System.IO.Compression.ZipArchive(
            new MemoryStream(await instrumentZipResponse.Content.ReadAsByteArrayAsync())))
        {
            Assert.Single(archive.Entries);
        }

        var voiceZipResponse = await anon.GetAsync($"/api/public/seasons/{token}/download?scorePartType=voice_part");
        Assert.Equal(HttpStatusCode.OK, voiceZipResponse.StatusCode);
        using (var archive = new System.IO.Compression.ZipArchive(
            new MemoryStream(await voiceZipResponse.Content.ReadAsByteArrayAsync())))
        {
            Assert.Single(archive.Entries);
        }
    }

    [Fact]
    public async Task Download_FilterByScorePartType_ReturnsZip()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLFilter");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLFilter");
        var token = await ConfigureShareAsync(client, season.Id);

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download?scorePartType=conductor_score");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Download_InvalidToken_Returns404()
    {
        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync("/api/public/seasons/invalidtoken_dl_999/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_WrongPassword_Returns401()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLPw");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLPw");

        var shareResponse = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share", new
        {
            IncludePdf = true,
            IncludeNotation = false,
            IncludePlayback = false,
            Password = "correctpass",
        });
        shareResponse.EnsureSuccessStatusCode();
        var body = await shareResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("token").GetString()!;

        var anon = GetUnauthenticatedClient();
        anon.DefaultRequestHeaders.Add("X-Share-Password", "wrongpass");
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Download_InvalidScorePartType_Returns400()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLBadType");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLBadType");
        var token = await ConfigureShareAsync(client, season.Id);

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download?scorePartType=malicious_injection");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ───── Per-arrangement download ─────

    private async Task UploadPdfAsync(HttpClient client, int arrangementId, string fileName, string? scorePartType = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes($"pdf-{fileName}"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);
        if (scorePartType != null)
            content.Add(new StringContent(scorePartType), "scorePartType");
        var response = await client.PostAsync($"/api/arrangements/{arrangementId}/files", content);
        response.EnsureSuccessStatusCode();
    }

    private static List<string> ZipEntryNames(byte[] zipBytes)
    {
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(zipBytes));
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    [Fact]
    public async Task Download_ByArrangement_ReturnsOnlyThatArrangementsFiles()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLByArr");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLByArr");

        var arr1 = await CreateArrangementAsync(client, "Arr_One");
        var arr2 = await CreateArrangementAsync(client, "Arr_Two");
        await UploadPdfAsync(client, arr1, "one.pdf");
        await UploadPdfAsync(client, arr2, "two.pdf");
        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arr1}", null);
        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arr2}", null);

        var token = await ConfigureShareAsync(client, season.Id);
        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download?arrangementId={arr1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = ZipEntryNames(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(new[] { "one.pdf" }, entries);
        Assert.DoesNotContain(entries, e => e.Contains('/'));
    }

    [Fact]
    public async Task Download_ByArrangement_InvalidArrangementId_Returns400()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLBadArr");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLBadArr");
        var token = await ConfigureShareAsync(client, season.Id);

        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download?arrangementId=999999");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Download_ByArrangementAndScorePartType_ReturnsZip()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLArrType");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLArrType");

        var arr = await CreateArrangementAsync(client, "Arr_Conductor");
        await UploadPdfAsync(client, arr, "score.pdf", "conductor_score");
        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arr}", null);

        var token = await ConfigureShareAsync(client, season.Id);
        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download?scorePartType=conductor_score&arrangementId={arr}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        var entries = ZipEntryNames(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(new[] { "score.pdf" }, entries);
    }

    [Fact]
    public async Task PrepareDownload_ByArrangement_ThenDownload()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLArrPrep");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLArrPrep");

        var arr = await CreateArrangementAsync(client, "Arr_Prep");
        await UploadPdfAsync(client, arr, "prep.pdf");
        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arr}", null);

        var token = await ConfigureShareAsync(client, season.Id);
        var anon = GetUnauthenticatedClient();

        var prepare = await anon.PostAsync($"/api/public/seasons/{token}/prepare-download?arrangementId={arr}", null);
        Assert.Equal(HttpStatusCode.NoContent, prepare.StatusCode);

        var download = await anon.GetAsync($"/api/public/seasons/{token}/download?arrangementId={arr}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(new[] { "prep.pdf" }, ZipEntryNames(await download.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task GetPublicSeason_IncludesArrangementIdAndCounts()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_ArrCounts");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_ArrCounts");

        var arr = await CreateArrangementAsync(client, "Arr_Counts");
        await UploadPdfAsync(client, arr, "a.pdf", "conductor_score");
        await UploadPdfAsync(client, arr, "b.pdf", "conductor_score");
        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arr}", null);

        var token = await ConfigureShareAsync(client, season.Id);
        var anon = GetUnauthenticatedClient();
        var body = await (await anon.GetAsync($"/api/public/seasons/{token}")).Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        var arrangements = body.GetProperty("arrangements").EnumerateArray().ToList();
        Assert.Equal(arr, arrangements[0].GetProperty("id").GetInt32());

        var conductor = body.GetProperty("downloadSections").EnumerateArray()
            .First(s => s.GetProperty("label").GetString() == "Conductor's Score");
        Assert.Equal(2, conductor.GetProperty("arrangementFileCounts").GetProperty(arr.ToString()).GetInt32());
    }

    [Fact]
    public async Task Download_ByArrangement_NoFiles_ReturnsValidEmptyZip()
    {
        var client = await GetLibrarianClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "Ens_DLArrEmpty");
        var season = await CreateSeasonAsync(client, ensemble.Id, "Season_DLArrEmpty");

        var arr = await CreateArrangementAsync(client, "Arr_Empty");
        await client.PostAsync($"/api/seasons/{season.Id}/arrangements/{arr}", null);

        var token = await ConfigureShareAsync(client, season.Id);
        var anon = GetUnauthenticatedClient();
        var response = await anon.GetAsync($"/api/public/seasons/{token}/download?arrangementId={arr}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
        Assert.Empty(ZipEntryNames(bytes));
    }
}
