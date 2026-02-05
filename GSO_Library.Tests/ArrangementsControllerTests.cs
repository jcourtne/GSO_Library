using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class ArrangementsControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ArrangementsControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Arrangement> CreateArrangementAsync(HttpClient client, string name = "Test Arrangement")
    {
        var response = await client.PostAsJsonAsync("/api/arrangements", new ArrangementRequest
        {
            Name = name,
            Description = "Test description",
            Arranger = "Test Arranger",
            Composer = "Test Composer",
            Key = "C Major",
            DurationSeconds = 300,
            Year = 2024
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Arrangement>(JsonOpts))!;
    }

    private async Task<int> CreateSeriesAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/series", new { Name = "Arr_TestSeries" });
        var series = await response.Content.ReadFromJsonAsync<Series>(JsonOpts);
        return series!.Id;
    }

    private async Task<int> CreateGameAsync(HttpClient client, int seriesId)
    {
        var response = await client.PostAsJsonAsync("/api/games", new
        {
            Name = "Arr_TestGame",
            SeriesId = seriesId
        });
        var game = await response.Content.ReadFromJsonAsync<Game>(JsonOpts);
        return game!.Id;
    }

    private async Task<int> CreateInstrumentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/instruments", new { Name = "Arr_Piano" });
        var inst = await response.Content.ReadFromJsonAsync<Instrument>(JsonOpts);
        return inst!.Id;
    }

    private async Task<int> CreatePerformanceAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Arr_Concert",
            Link = "https://example.com/arr"
        });
        var perf = await response.Content.ReadFromJsonAsync<Performance>(JsonOpts);
        return perf!.Id;
    }

    // ───── Green cases: CRUD ─────

    [Fact]
    public async Task CreateArrangement_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/arrangements", new ArrangementRequest
        {
            Name = "One Winged Angel",
            Composer = "Nobuo Uematsu",
            Key = "E Minor"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var arrangement = await response.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Equal("One Winged Angel", arrangement!.Name);
        Assert.True(arrangement.Id > 0);
    }

    [Fact]
    public async Task GetAllArrangements_WithPagination_Returns200()
    {
        var client = await GetEditorClientAsync();
        await CreateArrangementAsync(client, "Paginated1");
        await CreateArrangementAsync(client, "Paginated2");

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync("/api/arrangements?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Arrangement>>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal(1, result!.PageSize);
        Assert.True(result.TotalCount >= 2);
    }

    [Fact]
    public async Task GetArrangementById_Exists_Returns200()
    {
        var client = await GetEditorClientAsync();
        var created = await CreateArrangementAsync(client, "GetById");

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/arrangements/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var arrangement = await response.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Equal("GetById", arrangement!.Name);
    }

    [Fact]
    public async Task UpdateArrangementDetails_AsEditor_Returns200()
    {
        var client = await GetEditorClientAsync();
        var created = await CreateArrangementAsync(client);

        var response = await client.PutAsJsonAsync($"/api/arrangements/{created.Id}/details", new ArrangementRequest
        {
            Name = "Updated Name",
            Composer = "Updated Composer",
            Key = "D Minor"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal("Updated Composer", updated.Composer);
    }

    [Fact]
    public async Task DeleteArrangement_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();
        var created = await CreateArrangementAsync(client, "ToBeDeleted");

        var response = await client.DeleteAsync($"/api/arrangements/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify gone
        var getResponse = await client.GetAsync($"/api/arrangements/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Green cases: Relationship management ─────

    [Fact]
    public async Task AddAndRemoveGame_Succeeds()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);
        var seriesId = await CreateSeriesAsync(client);
        var gameId = await CreateGameAsync(client, seriesId);

        // Add game
        var addResponse = await client.PostAsync(
            $"/api/arrangements/{arrangement.Id}/games/{gameId}", null);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        // Verify the game is linked
        var getResponse = await client.GetAsync($"/api/arrangements/{arrangement.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Contains(updated!.Games, g => g.Id == gameId);

        // Remove game
        var removeResponse = await client.DeleteAsync(
            $"/api/arrangements/{arrangement.Id}/games/{gameId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
    }

    [Fact]
    public async Task AddAndRemoveInstrument_Succeeds()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);
        var instrumentId = await CreateInstrumentAsync(client);

        // Add instrument
        var addResponse = await client.PostAsync(
            $"/api/arrangements/{arrangement.Id}/instruments/{instrumentId}", null);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        // Verify linked
        var getResponse = await client.GetAsync($"/api/arrangements/{arrangement.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Contains(updated!.Instruments, i => i.Id == instrumentId);

        // Remove instrument
        var removeResponse = await client.DeleteAsync(
            $"/api/arrangements/{arrangement.Id}/instruments/{instrumentId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
    }

    [Fact]
    public async Task AddAndRemovePerformance_Succeeds()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);
        var performanceId = await CreatePerformanceAsync(client);

        // Add performance
        var addResponse = await client.PostAsync(
            $"/api/arrangements/{arrangement.Id}/performances/{performanceId}", null);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        // Verify linked
        var getResponse = await client.GetAsync($"/api/arrangements/{arrangement.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Contains(updated!.Performances, p => p.Id == performanceId);

        // Remove performance
        var removeResponse = await client.DeleteAsync(
            $"/api/arrangements/{arrangement.Id}/performances/{performanceId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
    }

    // ───── Green cases: File operations ─────

    [Fact]
    public async Task UploadListAndDeleteFile_Succeeds()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);

        // Upload file
        var content = new MultipartFormDataContent();
        var fileBytes = "test file content"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test-sheet.pdf");

        var uploadResponse = await client.PostAsync(
            $"/api/arrangements/{arrangement.Id}/files", content);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var file = await uploadResponse.Content.ReadFromJsonAsync<ArrangementFile>(JsonOpts);
        Assert.Equal("test-sheet.pdf", file!.FileName);

        // List files
        var listResponse = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var files = await listResponse.Content.ReadFromJsonAsync<List<ArrangementFile>>(JsonOpts);
        Assert.Contains(files!, f => f.Id == file.Id);

        // Download file
        var downloadResponse = await client.GetAsync(
            $"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileBytes, downloadedBytes);

        // Delete file
        var deleteResponse = await client.DeleteAsync(
            $"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    // ───── Green cases: Filtering ─────

    [Fact]
    public async Task GetArrangements_FilterByGame_ReturnsFiltered()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client, "Filtered");
        var seriesId = await CreateSeriesAsync(client);
        var gameId = await CreateGameAsync(client, seriesId);

        // Link game
        await client.PostAsync($"/api/arrangements/{arrangement.Id}/games/{gameId}", null);

        // Filter
        var response = await client.GetAsync($"/api/arrangements?gameId={gameId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Arrangement>>(JsonOpts);
        Assert.True(result!.TotalCount >= 1);
        Assert.All(result.Items, a => Assert.Contains(a.Games, g => g.Id == gameId));
    }

    // ───── Sorting ─────

    [Fact]
    public async Task GetArrangements_SortByNameAsc_ReturnsSorted()
    {
        var client = await GetEditorClientAsync();
        await CreateArrangementAsync(client, "ZZZ_Last");
        await CreateArrangementAsync(client, "AAA_First");

        var response = await client.GetAsync("/api/arrangements?sortBy=name&sortDirection=asc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Arrangement>>(JsonOpts);
        var names = result!.Items.Select(a => a.Name).ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public async Task GetArrangements_SortByNameDesc_ReturnsSorted()
    {
        var client = await GetEditorClientAsync();
        await CreateArrangementAsync(client, "AAA_Sort");
        await CreateArrangementAsync(client, "ZZZ_Sort");

        var response = await client.GetAsync("/api/arrangements?sortBy=name&sortDirection=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Arrangement>>(JsonOpts);
        var names = result!.Items.Select(a => a.Name).ToList();
        Assert.Equal(names.OrderByDescending(n => n).ToList(), names);
    }

    // ───── Audit fields ─────

    [Fact]
    public async Task CreateArrangement_SetsAuditFields()
    {
        var client = await GetEditorClientAsync();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var response = await client.PostAsJsonAsync("/api/arrangements", new ArrangementRequest
        {
            Name = "Audit Test"
        });
        response.EnsureSuccessStatusCode();
        var arrangement = await response.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);

        Assert.NotEqual(default, arrangement!.CreatedAt);
        Assert.NotEqual(default, arrangement.UpdatedAt);
        Assert.Equal("testeditor", arrangement.CreatedBy);
        Assert.True(arrangement.CreatedAt >= before);
    }

    // ───── File upload validation ─────

    [Fact]
    public async Task UploadFile_DisallowedExtension_Returns400()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client, "FileValExt");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("data"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "malicious.exe");

        var response = await client.PostAsync($"/api/arrangements/{arrangement.Id}/files", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(".exe", body);
    }

    [Fact]
    public async Task UploadFile_AllowedExtension_Returns201()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client, "FileValPdf");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("pdf data"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "score.pdf");

        var response = await client.PostAsync($"/api/arrangements/{arrangement.Id}/files", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_SetsCreatedBy()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client, "FileAudit");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("test"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "audit-test.pdf");

        var response = await client.PostAsync($"/api/arrangements/{arrangement.Id}/files", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var file = await response.Content.ReadFromJsonAsync<ArrangementFile>(JsonOpts);
        Assert.Equal("testeditor", file!.CreatedBy);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetArrangementById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();

        var response = await client.GetAsync("/api/arrangements/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateArrangement_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PutAsJsonAsync("/api/arrangements/99999/details", new ArrangementRequest
        {
            Name = "Nope"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteArrangement_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.DeleteAsync("/api/arrangements/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddGame_ToNonexistentArrangement_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsync("/api/arrangements/99999/games/1", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddInstrument_ToNonexistentArrangement_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsync("/api/arrangements/99999/instruments/1", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddPerformance_ToNonexistentArrangement_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsync("/api/arrangements/99999/performances/1", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateArrangement_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/arrangements", new ArrangementRequest
        {
            Name = "Nope"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateArrangement_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/arrangements", new ArrangementRequest
        {
            Name = "Nope"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ToNonexistentArrangement_Returns404()
    {
        var client = await GetEditorClientAsync();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("data"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.pdf");

        var response = await client.PostAsync("/api/arrangements/99999/files", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);

        var response = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListFiles_ForNonexistentArrangement_Returns404()
    {
        var client = await GetUserClientAsync();

        var response = await client.GetAsync("/api/arrangements/99999/files");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
