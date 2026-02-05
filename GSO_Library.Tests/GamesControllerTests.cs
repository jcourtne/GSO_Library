using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class GamesControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GamesControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<int> CreateSeriesAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/series", new { Name = "TestSeries_Games" });
        var series = await response.Content.ReadFromJsonAsync<Series>(JsonOpts);
        return series!.Id;
    }

    // ───── Green cases ─────

    [Fact]
    public async Task CreateGame_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();
        var seriesId = await CreateSeriesAsync(client);

        var response = await client.PostAsJsonAsync("/api/games", new
        {
            Name = "Final Fantasy VII",
            Description = "Classic RPG",
            SeriesId = seriesId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<Game>(JsonOpts);
        Assert.Equal("Final Fantasy VII", game!.Name);
        Assert.True(game.Id > 0);
    }

    [Fact]
    public async Task GetAllGames_Authenticated_Returns200()
    {
        var client = await GetEditorClientAsync();
        var seriesId = await CreateSeriesAsync(client);
        await client.PostAsJsonAsync("/api/games", new { Name = "TestGame", SeriesId = seriesId });

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync("/api/games");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Game>>(JsonOpts);
        Assert.NotNull(result);
        Assert.True(result!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetGameById_Exists_Returns200()
    {
        var client = await GetEditorClientAsync();
        var seriesId = await CreateSeriesAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/games", new
        {
            Name = "Ocarina of Time",
            SeriesId = seriesId
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Game>(JsonOpts);

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/games/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<Game>(JsonOpts);
        Assert.Equal("Ocarina of Time", game!.Name);
    }

    [Fact]
    public async Task UpdateGame_AsEditor_Returns200()
    {
        var client = await GetEditorClientAsync();
        var seriesId = await CreateSeriesAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/games", new
        {
            Name = "OldName",
            SeriesId = seriesId
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Game>(JsonOpts);

        var response = await client.PutAsJsonAsync($"/api/games/{created!.Id}", new
        {
            Name = "NewName",
            Description = "Updated description",
            SeriesId = seriesId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Game>(JsonOpts);
        Assert.Equal("NewName", updated!.Name);
    }

    [Fact]
    public async Task DeleteGame_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();
        var seriesId = await CreateSeriesAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/games", new
        {
            Name = "ToDelete",
            SeriesId = seriesId
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Game>(JsonOpts);

        var response = await client.DeleteAsync($"/api/games/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify gone
        var getResponse = await client.GetAsync($"/api/games/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Sorting ─────

    [Fact]
    public async Task GetAllGames_SortByNameDesc_ReturnsSorted()
    {
        var client = await GetEditorClientAsync();
        var seriesId = await CreateSeriesAsync(client);
        await client.PostAsJsonAsync("/api/games", new { Name = "AAA_GameSort", SeriesId = seriesId });
        await client.PostAsJsonAsync("/api/games", new { Name = "ZZZ_GameSort", SeriesId = seriesId });

        var response = await client.GetAsync("/api/games?sortBy=name&sortDirection=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Game>>(JsonOpts);
        var names = result!.Items.Select(g => g.Name).ToList();
        Assert.Equal(names.OrderByDescending(n => n).ToList(), names);
    }

    // ───── Audit fields ─────

    [Fact]
    public async Task CreateGame_SetsAuditFields()
    {
        var client = await GetEditorClientAsync();
        var seriesId = await CreateSeriesAsync(client);

        var response = await client.PostAsJsonAsync("/api/games", new
        {
            Name = "AuditGame",
            SeriesId = seriesId
        });
        response.EnsureSuccessStatusCode();
        var game = await response.Content.ReadFromJsonAsync<Game>(JsonOpts);

        Assert.NotEqual(default, game!.CreatedAt);
        Assert.NotEqual(default, game.UpdatedAt);
        Assert.Equal("testeditor", game.CreatedBy);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetGameById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();

        var response = await client.GetAsync("/api/games/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateGame_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PutAsJsonAsync("/api/games/99999", new
        {
            Name = "Nope",
            SeriesId = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGame_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.DeleteAsync("/api/games/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateGame_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/games", new { Name = "Nope", SeriesId = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateGame_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/games", new { Name = "Nope", SeriesId = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
