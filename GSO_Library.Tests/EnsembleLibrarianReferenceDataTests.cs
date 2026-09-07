using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

/// <summary>
/// Ensemble Librarians can manage the shared reference data (games, series, instruments)
/// even though they are otherwise scoped to their own ensemble.
/// </summary>
public class EnsembleLibrarianReferenceDataTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public EnsembleLibrarianReferenceDataTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateAndUpdateSeries_AsEnsembleLibrarian_Succeeds()
    {
        var client = await GetEnsembleLibrarianClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/series", new { Name = "EnsLib Series" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var series = await createResp.Content.ReadFromJsonAsync<Series>(JsonOpts);

        var updateResp = await client.PutAsJsonAsync($"/api/series/{series!.Id}", new { Name = "EnsLib Series Renamed" });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/api/series/{series.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task CreateAndUpdateGame_AsEnsembleLibrarian_Succeeds()
    {
        var client = await GetEnsembleLibrarianClientAsync();

        var seriesResp = await client.PostAsJsonAsync("/api/series", new { Name = "EnsLib Game Series" });
        var series = await seriesResp.Content.ReadFromJsonAsync<Series>(JsonOpts);

        var createResp = await client.PostAsJsonAsync("/api/games", new { Name = "EnsLib Game", SeriesId = series!.Id });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var game = await createResp.Content.ReadFromJsonAsync<Game>(JsonOpts);

        var updateResp = await client.PutAsJsonAsync($"/api/games/{game!.Id}",
            new { Name = "EnsLib Game Renamed", SeriesId = series.Id });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/api/games/{game.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task CreateAndUpdateInstrument_AsEnsembleLibrarian_Succeeds()
    {
        var client = await GetEnsembleLibrarianClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/instruments", new { Name = "EnsLib Ocarina" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var instrument = await createResp.Content.ReadFromJsonAsync<Instrument>(JsonOpts);

        var updateResp = await client.PutAsJsonAsync($"/api/instruments/{instrument!.Id}",
            new { Name = "EnsLib Ocarina Renamed" });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/api/instruments/{instrument.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task CreateGame_AsEnsembleDownloader_Returns403()
    {
        var client = await GetEnsembleDownloaderClientAsync();

        var response = await client.PostAsJsonAsync("/api/games", new { Name = "Nope", SeriesId = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
