using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class EnsemblesControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public EnsemblesControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ───── Green cases ─────

    [Fact]
    public async Task CreateEnsemble_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/ensembles", new
        {
            Name = "GSO Ensemble",
            Description = "A video game music ensemble",
            Website = "https://gso.example.com",
            ContactInfo = "info@gso.example.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ensemble = await response.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);
        Assert.Equal("GSO Ensemble", ensemble!.Name);
        Assert.Equal("A video game music ensemble", ensemble.Description);
        Assert.Equal("https://gso.example.com", ensemble.Website);
        Assert.Equal("info@gso.example.com", ensemble.ContactInfo);
        Assert.True(ensemble.Id > 0);
    }

    [Fact]
    public async Task GetAllEnsembles_Authenticated_Returns200()
    {
        var client = await GetEditorClientAsync();
        await client.PostAsJsonAsync("/api/ensembles", new { Name = "ListEnsemble" });

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync("/api/ensembles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Ensemble>>(JsonOpts);
        Assert.NotNull(result);
        Assert.True(result!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetEnsembleById_Exists_Returns200()
    {
        var client = await GetEditorClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/ensembles", new
        {
            Name = "Detail Ensemble",
            Description = "Test description"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/ensembles/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ensemble = await response.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);
        Assert.Equal("Detail Ensemble", ensemble!.Name);
    }

    [Fact]
    public async Task UpdateEnsemble_AsEditor_Returns200()
    {
        var client = await GetEditorClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/ensembles", new { Name = "OldEnsemble" });
        var created = await createResponse.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);

        var response = await client.PutAsJsonAsync($"/api/ensembles/{created!.Id}", new
        {
            Name = "Updated Ensemble",
            Description = "Updated description",
            Website = "https://updated.example.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);
        Assert.Equal("Updated Ensemble", updated!.Name);
        Assert.Equal("Updated description", updated.Description);
    }

    [Fact]
    public async Task DeleteEnsemble_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/ensembles", new { Name = "ToDeleteEnsemble" });
        var created = await createResponse.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);

        var response = await client.DeleteAsync($"/api/ensembles/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify gone
        var getResponse = await client.GetAsync($"/api/ensembles/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Sorting ─────

    [Fact]
    public async Task GetAllEnsembles_SortByNameDesc_ReturnsSorted()
    {
        var client = await GetEditorClientAsync();
        await client.PostAsJsonAsync("/api/ensembles", new { Name = "AAA_EnsembleSort" });
        await client.PostAsJsonAsync("/api/ensembles", new { Name = "ZZZ_EnsembleSort" });

        var response = await client.GetAsync("/api/ensembles?sortBy=name&sortDirection=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Ensemble>>(JsonOpts);
        var names = result!.Items.Select(e => e.Name).ToList();
        Assert.Equal(names.OrderByDescending(n => n).ToList(), names);
    }

    // ───── Audit fields ─────

    [Fact]
    public async Task CreateEnsemble_SetsAuditFields()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/ensembles", new { Name = "AuditEnsemble" });
        response.EnsureSuccessStatusCode();
        var ensemble = await response.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);

        Assert.NotEqual(default, ensemble!.CreatedAt);
        Assert.NotEqual(default, ensemble.UpdatedAt);
        Assert.Equal("testeditor", ensemble.CreatedBy);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetEnsembleById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();

        var response = await client.GetAsync("/api/ensembles/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEnsemble_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PutAsJsonAsync("/api/ensembles/99999", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEnsemble_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.DeleteAsync("/api/ensembles/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateEnsemble_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/ensembles", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEnsemble_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/ensembles", new { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ───── Ensemble-Performance FK relationship ─────

    [Fact]
    public async Task CreatePerformanceWithEnsemble_ReturnsEnsembleId()
    {
        var client = await GetEditorClientAsync();

        // Create ensemble
        var ensembleResponse = await client.PostAsJsonAsync("/api/ensembles", new { Name = "FK Test Ensemble" });
        var ensemble = await ensembleResponse.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);

        // Create performance linked to ensemble
        var perfResponse = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "FK Test Performance",
            Link = "https://example.com/fk",
            EnsembleId = ensemble!.Id
        });

        Assert.Equal(HttpStatusCode.Created, perfResponse.StatusCode);
        var perf = await perfResponse.Content.ReadFromJsonAsync<Performance>(JsonOpts);
        Assert.Equal(ensemble.Id, perf!.EnsembleId);
    }

    [Fact]
    public async Task DeleteEnsemble_SetsPerformanceEnsembleIdToNull()
    {
        var client = await GetAdminClientAsync();

        // Create ensemble
        var ensembleResponse = await client.PostAsJsonAsync("/api/ensembles", new { Name = "Cascade Test Ensemble" });
        var ensemble = await ensembleResponse.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);

        // Create performance linked to ensemble
        var perfResponse = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Cascade Test Performance",
            Link = "https://example.com/cascade",
            EnsembleId = ensemble!.Id
        });
        var perf = await perfResponse.Content.ReadFromJsonAsync<Performance>(JsonOpts);

        // Delete ensemble
        var deleteResponse = await client.DeleteAsync($"/api/ensembles/{ensemble.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify performance still exists but ensemble_id is null
        var getResponse = await client.GetAsync($"/api/performances/{perf!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var updatedPerf = await getResponse.Content.ReadFromJsonAsync<Performance>(JsonOpts);
        Assert.Null(updatedPerf!.EnsembleId);
    }
}
