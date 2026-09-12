using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

/// <summary>
/// An Ensemble Librarian may create/update/delete seasons and performances, but only those
/// tied to an ensemble they are a member of.
/// </summary>
public class EnsembleLibrarianScopedWriteTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public EnsembleLibrarianScopedWriteTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Ensemble> CreateEnsembleAsync(HttpClient admin, string name)
    {
        var resp = await admin.PostAsJsonAsync("/api/ensembles", new { Name = name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Ensemble>(JsonOpts))!;
    }

    private async Task<string> GetUserIdAsync(HttpClient admin, string username)
    {
        var resp = await admin.GetAsync("/api/auth/users");
        resp.EnsureSuccessStatusCode();
        var users = await resp.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        return users!.First(u => u.UserName == username).Id;
    }

    /// <summary>Creates an ensemble and adds the ensemble-librarian test user as a member.</summary>
    private async Task<Ensemble> CreateEnsembleWithLibrarianAsync(HttpClient admin, string name)
    {
        var ensemble = await CreateEnsembleAsync(admin, name);
        var ensLibId = await GetUserIdAsync(admin, "testensemblelibrarian");
        (await admin.PostAsync($"/api/ensembles/{ensemble.Id}/members/{ensLibId}", null)).EnsureSuccessStatusCode();
        return ensemble;
    }

    // ───── Seasons ─────

    [Fact]
    public async Task Season_FullLifecycle_InOwnEnsemble_Succeeds()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibSeasonOwn");
        var client = await GetEnsembleLibrarianClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/seasons",
            new { Name = "EnsLib Season", EnsembleId = ensemble.Id });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var season = await createResp.Content.ReadFromJsonAsync<Season>(JsonOpts);

        var updateResp = await client.PutAsJsonAsync($"/api/seasons/{season!.Id}",
            new { Name = "EnsLib Season Renamed", EnsembleId = ensemble.Id });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var shareResp = await client.PostAsJsonAsync($"/api/seasons/{season.Id}/share",
            new { IncludePdf = true, IncludeNotation = false, IncludePlayback = false });
        Assert.Equal(HttpStatusCode.OK, shareResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/api/seasons/{season.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task Season_Create_ForOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var otherEnsemble = await CreateEnsembleAsync(admin, "EnsLibSeasonOther");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.PostAsJsonAsync("/api/seasons",
            new { Name = "ShouldFail", EnsembleId = otherEnsemble.Id });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Season_Update_InOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var otherEnsemble = await CreateEnsembleAsync(admin, "EnsLibSeasonUpdOther");
        var createResp = await admin.PostAsJsonAsync("/api/seasons",
            new { Name = "AdminSeason", EnsembleId = otherEnsemble.Id });
        var season = await createResp.Content.ReadFromJsonAsync<Season>(JsonOpts);

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.PutAsJsonAsync($"/api/seasons/{season!.Id}",
            new { Name = "ShouldFail", EnsembleId = otherEnsemble.Id });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Season_Update_MovingIntoOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var ownEnsemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibSeasonMoveOwn");
        var otherEnsemble = await CreateEnsembleAsync(admin, "EnsLibSeasonMoveOther");
        var client = await GetEnsembleLibrarianClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/seasons",
            new { Name = "MoveMe", EnsembleId = ownEnsemble.Id });
        var season = await createResp.Content.ReadFromJsonAsync<Season>(JsonOpts);

        var resp = await client.PutAsJsonAsync($"/api/seasons/{season!.Id}",
            new { Name = "MoveMe", EnsembleId = otherEnsemble.Id });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ───── Performances ─────

    [Fact]
    public async Task Performance_FullLifecycle_InOwnEnsemble_Succeeds()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibPerfOwn");
        var client = await GetEnsembleLibrarianClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/performances",
            new { Name = "EnsLib Perf", Link = "https://example.com/p", EnsembleId = ensemble.Id });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var performance = await createResp.Content.ReadFromJsonAsync<Performance>(JsonOpts);

        var updateResp = await client.PutAsJsonAsync($"/api/performances/{performance!.Id}",
            new { Name = "EnsLib Perf Renamed", Link = "https://example.com/p", EnsembleId = ensemble.Id });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/api/performances/{performance.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task Performance_Create_ForOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var otherEnsemble = await CreateEnsembleAsync(admin, "EnsLibPerfOther");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.PostAsJsonAsync("/api/performances",
            new { Name = "ShouldFail", EnsembleId = otherEnsemble.Id });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Performance_Create_WithNoEnsemble_Returns403()
    {
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.PostAsJsonAsync("/api/performances", new { Name = "ShouldFail", Link = "https://example.com/p" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Performance_Delete_InOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var otherEnsemble = await CreateEnsembleAsync(admin, "EnsLibPerfDelOther");
        var createResp = await admin.PostAsJsonAsync("/api/performances",
            new { Name = "AdminPerf", Link = "https://example.com/p", EnsembleId = otherEnsemble.Id });
        var performance = await createResp.Content.ReadFromJsonAsync<Performance>(JsonOpts);

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.DeleteAsync($"/api/performances/{performance!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ───── Season arrangements ─────

    private async Task<int> CreateArrangementAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/arrangements", new { Name = name });
        resp.EnsureSuccessStatusCode();
        var arr = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return arr.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task AddArrangement_ArrangementInOwnEnsemble_Succeeds()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibSeasonArrOwn");
        var arrangementId = await CreateArrangementAsync(admin, "EnsLibSeasonArrOwn_Arr");
        (await admin.PostAsync($"/api/arrangements/{arrangementId}/ensembles/{ensemble.Id}", null))
            .EnsureSuccessStatusCode();

        var client = await GetEnsembleLibrarianClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/seasons",
            new { Name = "EnsLib Season ArrOwn", EnsembleId = ensemble.Id });
        var season = await createResp.Content.ReadFromJsonAsync<Season>(JsonOpts);

        var resp = await client.PostAsync($"/api/seasons/{season!.Id}/arrangements/{arrangementId}", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task AddArrangement_ArrangementNotInLibrariansEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibSeasonArrOther");
        var arrangementId = await CreateArrangementAsync(admin, "EnsLibSeasonArrOther_Arr");
        // Link the arrangement to a different ensemble so it is not public.
        var otherEnsembleResp = await admin.PostAsJsonAsync("/api/ensembles", new { Name = "EnsLibSeasonArrOther_Foreign" });
        var otherEnsemble = await otherEnsembleResp.Content.ReadFromJsonAsync<Ensemble>(JsonOpts);
        (await admin.PostAsync($"/api/arrangements/{arrangementId}/ensembles/{otherEnsemble!.Id}", null))
            .EnsureSuccessStatusCode();

        var client = await GetEnsembleLibrarianClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/seasons",
            new { Name = "EnsLib Season ArrOther", EnsembleId = ensemble.Id });
        var season = await createResp.Content.ReadFromJsonAsync<Season>(JsonOpts);

        var resp = await client.PostAsync($"/api/seasons/{season!.Id}/arrangements/{arrangementId}", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ───── Ensembles ─────

    [Fact]
    public async Task Ensemble_Update_OwnEnsemble_Succeeds()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibOwnEnsemble");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.PutAsJsonAsync($"/api/ensembles/{ensemble.Id}",
            new { Name = "EnsLibOwnEnsemble Renamed", Description = "Updated by the ensemble librarian" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Ensemble_Update_OtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var otherEnsemble = await CreateEnsembleAsync(admin, "EnsLibOtherEnsemble");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.PutAsJsonAsync($"/api/ensembles/{otherEnsemble.Id}",
            new { Name = "ShouldFail" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Ensemble_Create_AsEnsembleLibrarian_Returns403()
    {
        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.PostAsJsonAsync("/api/ensembles", new { Name = "ShouldFail" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Ensemble_Delete_OwnEnsemble_AsEnsembleLibrarian_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibDeleteOwnEnsemble");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.DeleteAsync($"/api/ensembles/{ensemble.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ───── Public arrangements ─────
    // An Ensemble Librarian can edit any public arrangement even without ownership or shared
    // ensemble membership, but deleting one or linking an ensemble to it is still reserved for
    // the actual owner.

    [Fact]
    public async Task UpdateDetails_PublicArrangement_AsEnsembleLibrarian_NonOwner_Succeeds()
    {
        var admin = await GetAdminClientAsync();
        var arrangementId = await CreateArrangementAsync(admin, "EnsLibPublicArrUpdate");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.PutAsJsonAsync($"/api/arrangements/{arrangementId}/details",
            new ArrangementRequest { Name = "EnsLibPublicArrUpdate Renamed" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteArrangement_PublicArrangement_AsEnsembleLibrarian_NonOwner_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var arrangementId = await CreateArrangementAsync(admin, "EnsLibPublicArrDeleteNonOwner");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.DeleteAsync($"/api/arrangements/{arrangementId}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteArrangement_PublicArrangement_AsEnsembleLibrarian_Owner_Returns204()
    {
        var client = await GetEnsembleLibrarianClientAsync();
        var arrangementId = await CreateArrangementAsync(client, "EnsLibPublicArrDeleteOwner");

        var resp = await client.DeleteAsync($"/api/arrangements/{arrangementId}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_PublicArrangement_AsEnsembleLibrarian_NonOwner_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var arrangementId = await CreateArrangementAsync(admin, "EnsLibPublicArrLinkNonOwner");
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibPublicArrLinkNonOwnerEnsemble");
        var client = await GetEnsembleLibrarianClientAsync();

        var resp = await client.PostAsync($"/api/arrangements/{arrangementId}/ensembles/{ensemble.Id}", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_PublicArrangement_AsEnsembleLibrarian_Owner_Returns204()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleWithLibrarianAsync(admin, "EnsLibPublicArrLinkOwnerEnsemble");
        var client = await GetEnsembleLibrarianClientAsync();
        var arrangementId = await CreateArrangementAsync(client, "EnsLibPublicArrLinkOwner");

        var resp = await client.PostAsync($"/api/arrangements/{arrangementId}/ensembles/{ensemble.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }
}
