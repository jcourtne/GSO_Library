using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class SeriesControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SeriesControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ───── Green cases ─────

    [Fact]
    public async Task CreateSeries_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/series", new
        {
            Name = "Final Fantasy",
            Description = "Classic JRPG series"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var series = await response.Content.ReadFromJsonAsync<Series>(JsonOpts);
        Assert.Equal("Final Fantasy", series!.Name);
        Assert.True(series.Id > 0);
    }

    [Fact]
    public async Task GetAllSeries_Authenticated_Returns200()
    {
        var client = await GetUserClientAsync();

        // Seed a series first
        var editorClient = await GetEditorClientAsync();
        await editorClient.PostAsJsonAsync("/api/series", new { Name = "Zelda" });

        var response = await client.GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Series>>(JsonOpts);
        Assert.NotNull(result);
        Assert.True(result!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetSeriesById_Exists_Returns200()
    {
        var client = await GetEditorClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/series", new
        {
            Name = "Mario",
            Description = "Nintendo platformer"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Series>(JsonOpts);

        // Get by ID
        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/series/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var series = await response.Content.ReadFromJsonAsync<Series>(JsonOpts);
        Assert.Equal("Mario", series!.Name);
    }

    [Fact]
    public async Task UpdateSeries_AsEditor_Returns200()
    {
        var client = await GetEditorClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/series", new { Name = "Sonic" });
        var created = await createResponse.Content.ReadFromJsonAsync<Series>(JsonOpts);

        // Update
        var response = await client.PutAsJsonAsync($"/api/series/{created!.Id}", new
        {
            Name = "Sonic the Hedgehog",
            Description = "Sega's mascot"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Series>(JsonOpts);
        Assert.Equal("Sonic the Hedgehog", updated!.Name);
    }

    [Fact]
    public async Task DeleteSeries_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/series", new { Name = "ToDelete" });
        var created = await createResponse.Content.ReadFromJsonAsync<Series>(JsonOpts);

        // Delete
        var response = await client.DeleteAsync($"/api/series/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify gone
        var getResponse = await client.GetAsync($"/api/series/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetSeriesById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();

        var response = await client.GetAsync("/api/series/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSeries_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PutAsJsonAsync("/api/series/99999", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSeries_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.DeleteAsync("/api/series/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeries_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/series", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeries_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/series", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
