using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class InstrumentsControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public InstrumentsControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ───── Green cases ─────

    [Fact]
    public async Task CreateInstrument_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/instruments", new { Name = "Violin" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var instrument = await response.Content.ReadFromJsonAsync<Instrument>(JsonOpts);
        Assert.Equal("Violin", instrument!.Name);
        Assert.True(instrument.Id > 0);
    }

    [Fact]
    public async Task GetAllInstruments_Authenticated_Returns200()
    {
        var client = await GetEditorClientAsync();
        await client.PostAsJsonAsync("/api/instruments", new { Name = "Flute" });

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync("/api/instruments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Instrument>>(JsonOpts);
        Assert.NotNull(result);
        Assert.True(result!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetInstrumentById_Exists_Returns200()
    {
        var client = await GetEditorClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/instruments", new { Name = "Trumpet" });
        var created = await createResponse.Content.ReadFromJsonAsync<Instrument>(JsonOpts);

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/instruments/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var instrument = await response.Content.ReadFromJsonAsync<Instrument>(JsonOpts);
        Assert.Equal("Trumpet", instrument!.Name);
    }

    [Fact]
    public async Task UpdateInstrument_AsEditor_Returns200()
    {
        var client = await GetEditorClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/instruments", new { Name = "Celo" });
        var created = await createResponse.Content.ReadFromJsonAsync<Instrument>(JsonOpts);

        var response = await client.PutAsJsonAsync($"/api/instruments/{created!.Id}", new
        {
            Name = "Cello"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Instrument>(JsonOpts);
        Assert.Equal("Cello", updated!.Name);
    }

    [Fact]
    public async Task DeleteInstrument_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/instruments", new { Name = "Oboe" });
        var created = await createResponse.Content.ReadFromJsonAsync<Instrument>(JsonOpts);

        var response = await client.DeleteAsync($"/api/instruments/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify gone
        var getResponse = await client.GetAsync($"/api/instruments/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetInstrumentById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();

        var response = await client.GetAsync("/api/instruments/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateInstrument_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PutAsJsonAsync("/api/instruments/99999", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteInstrument_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.DeleteAsync("/api/instruments/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateInstrument_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/instruments", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInstrument_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/instruments", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
