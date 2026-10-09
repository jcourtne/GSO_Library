using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class GroupInstrumentationsControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GroupInstrumentationsControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<GroupInstrumentation> CreateGroupInstrumentationAsync(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/group-instrumentations",
            new { Name = name ?? $"GI_{Guid.NewGuid()}", Description = "Test description" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupInstrumentation>(JsonOpts))!;
    }

    private async Task<Arrangement> CreateArrangementAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/arrangements", new ArrangementRequest { Name = $"GI_Arr_{Guid.NewGuid()}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Arrangement>(JsonOpts))!;
    }

    // ───── Green cases: CRUD ─────

    [Fact]
    public async Task CreateGroupInstrumentation_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();
        var name = $"Full Orchestra {Guid.NewGuid()}";

        var response = await client.PostAsJsonAsync("/api/group-instrumentations", new { Name = name, Description = "Strings, winds, brass, percussion" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<GroupInstrumentation>(JsonOpts);
        Assert.Equal(name, created!.Name);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task CreateAndUpdateAndDelete_AsEnsembleLibrarian_Succeeds()
    {
        var client = await GetEnsembleLibrarianClientAsync();
        var created = await CreateGroupInstrumentationAsync(client);

        var updateResp = await client.PutAsJsonAsync($"/api/group-instrumentations/{created.Id}",
            new { Name = $"Renamed_{Guid.NewGuid()}", Description = "Updated" });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/api/group-instrumentations/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task GetById_AsUser_ReturnsLinkedArrangements()
    {
        var editor = await GetEditorClientAsync();
        var gi = await CreateGroupInstrumentationAsync(editor);
        var arrangement = await CreateArrangementAsync(editor);
        await editor.PostAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/{gi.Id}", null);

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/group-instrumentations/{gi.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<GroupInstrumentation>(JsonOpts);
        Assert.Contains(fetched!.Arrangements, a => a.Id == arrangement.Id);
    }

    [Fact]
    public async Task GetAll_WithSearch_ReturnsMatchesOnly()
    {
        var client = await GetEditorClientAsync();
        var token = Guid.NewGuid().ToString("N");
        await CreateGroupInstrumentationAsync(client, $"Quartet {token}");
        await CreateGroupInstrumentationAsync(client);

        var response = await client.GetAsync($"/api/group-instrumentations?search={token.ToUpperInvariant()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<GroupInstrumentation>>(JsonOpts);
        Assert.Equal(1, result!.TotalCount);
        Assert.Contains(token, result.Items.Single().Name);
    }

    // ───── Red cases ─────

    [Fact]
    public async Task Create_DuplicateNameCaseInsensitive_Returns409()
    {
        var client = await GetEditorClientAsync();
        var name = $"Brass Band {Guid.NewGuid()}";
        await CreateGroupInstrumentationAsync(client, name);

        var response = await client.PostAsJsonAsync("/api/group-instrumentations", new { Name = name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_DuplicateName_Returns409()
    {
        var client = await GetEditorClientAsync();
        var first = await CreateGroupInstrumentationAsync(client);
        var second = await CreateGroupInstrumentationAsync(client);

        var response = await client.PutAsJsonAsync($"/api/group-instrumentations/{second.Id}", new { Name = first.Name });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();
        var response = await client.GetAsync("/api/group-instrumentations/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var response = await client.PutAsJsonAsync("/api/group-instrumentations/99999", new { Name = $"Missing_{Guid.NewGuid()}" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var response = await client.DeleteAsync("/api/group-instrumentations/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsUser_Returns403()
    {
        var client = await GetUserClientAsync();
        var response = await client.PostAsJsonAsync("/api/group-instrumentations", new { Name = "Nope" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsDownloader_Returns403()
    {
        var client = await GetDownloaderClientAsync();
        var response = await client.PostAsJsonAsync("/api/group-instrumentations", new { Name = "Nope" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();
        var response = await client.GetAsync("/api/group-instrumentations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ───── Arrangement linking, filtering ─────

    [Fact]
    public async Task AddAndRemoveGroupInstrumentation_OnArrangement_Succeeds()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);
        var gi = await CreateGroupInstrumentationAsync(client);

        var addResponse = await client.PostAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/{gi.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        var linked = await (await client.GetAsync($"/api/arrangements/{arrangement.Id}")).Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Contains(linked!.GroupInstrumentations, g => g.Id == gi.Id);

        var duplicateAdd = await client.PostAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/{gi.Id}", null);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateAdd.StatusCode);

        var removeResponse = await client.DeleteAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/{gi.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var unlinked = await (await client.GetAsync($"/api/arrangements/{arrangement.Id}")).Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.DoesNotContain(unlinked!.GroupInstrumentations, g => g.Id == gi.Id);
    }

    [Fact]
    public async Task AddGroupInstrumentation_MissingArrangement_Returns404()
    {
        var client = await GetEditorClientAsync();
        var gi = await CreateGroupInstrumentationAsync(client);
        var response = await client.PostAsync($"/api/arrangements/99999/group-instrumentations/{gi.Id}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddGroupInstrumentation_MissingGroupInstrumentation_Returns400()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);
        var response = await client.PostAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/99999", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddGroupInstrumentation_AsUser_Returns403()
    {
        var editor = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(editor);
        var gi = await CreateGroupInstrumentationAsync(editor);

        var userClient = await GetUserClientAsync();
        var response = await userClient.PostAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/{gi.Id}", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetArrangements_FilterByGroupInstrumentation_ReturnsFiltered()
    {
        var client = await GetEditorClientAsync();
        var linked = await CreateArrangementAsync(client);
        var unlinked = await CreateArrangementAsync(client);
        var gi = await CreateGroupInstrumentationAsync(client);
        await client.PostAsync($"/api/arrangements/{linked.Id}/group-instrumentations/{gi.Id}", null);

        var response = await client.GetAsync($"/api/arrangements?groupInstrumentationIds={gi.Id}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Arrangement>>(JsonOpts);
        Assert.Contains(result!.Items, a => a.Id == linked.Id);
        Assert.DoesNotContain(result.Items, a => a.Id == unlinked.Id);
        Assert.All(result.Items, a => Assert.Contains(a.GroupInstrumentations, g => g.Id == gi.Id));
    }

    [Fact]
    public async Task FilterOptions_IncludesLinkedGroupInstrumentation()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);
        var gi = await CreateGroupInstrumentationAsync(client);
        await client.PostAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/{gi.Id}", null);

        var response = await client.GetAsync("/api/arrangements/filter-options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var options = doc.RootElement.GetProperty("groupInstrumentations").EnumerateArray();
        Assert.Contains(options, o => o.GetProperty("id").GetInt32() == gi.Id);
    }

    [Fact]
    public async Task DeleteGroupInstrumentation_RemovesItFromArrangement()
    {
        var client = await GetEditorClientAsync();
        var arrangement = await CreateArrangementAsync(client);
        var gi = await CreateGroupInstrumentationAsync(client);
        await client.PostAsync($"/api/arrangements/{arrangement.Id}/group-instrumentations/{gi.Id}", null);

        // Warm the arrangement cache, then delete — the deletion must invalidate it
        await client.GetAsync($"/api/arrangements/{arrangement.Id}");
        var deleteResp = await client.DeleteAsync($"/api/group-instrumentations/{gi.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var after = await (await client.GetAsync($"/api/arrangements/{arrangement.Id}")).Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.DoesNotContain(after!.GroupInstrumentations, g => g.Id == gi.Id);
    }
}
