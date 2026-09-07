using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class ArrangementEnsemblesControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ArrangementEnsemblesControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Arrangement> CreateArrangementAsync(HttpClient client, string name = "TestArrangement")
    {
        var resp = await client.PostAsJsonAsync("/api/arrangements", new { Name = name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts))!;
    }

    private async Task<Ensemble> CreateEnsembleAsync(HttpClient client, string name = "TestEnsemble")
    {
        var resp = await client.PostAsJsonAsync("/api/ensembles", new { Name = name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Ensemble>(JsonOpts))!;
    }

    private async Task<string> GetSubmitterUserIdAsync(HttpClient adminClient)
    {
        var resp = await adminClient.GetAsync("/api/auth/users");
        resp.EnsureSuccessStatusCode();
        var users = await resp.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        return users!.First(u => u.UserName == "testsubmitter").Id;
    }

    // ───── AddEnsemble ─────

    [Fact]
    public async Task AddEnsemble_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "AdminAddEnsembleArrangement");
        var ensemble = await CreateEnsembleAsync(client, "AdminAddEnsemble");

        var resp = await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_AsLibrarian_Returns204()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "LibrarianAddEnsemble");
        var libClient = await GetLibrarianClientAsync();
        var arrangement = await CreateArrangementAsync(libClient, "LibrarianAddEnsembleArrangement");

        var resp = await libClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_AsSubmitter_OwnerAndMember_Returns204()
    {
        var adminClient = await GetAdminClientAsync();
        var submitterId = await GetSubmitterUserIdAsync(adminClient);

        var submitterClient = await GetSubmitterClientAsync();
        var arrangement = await CreateArrangementAsync(submitterClient, "SubmitterOwnArrangement");

        // Now add the submitter to an ensemble and explicitly link it to the arrangement
        var ensemble = await CreateEnsembleAsync(adminClient, "SubmitterAddEnsemble");
        await adminClient.PostAsync($"/api/ensembles/{ensemble.Id}/members/{submitterId}", null);

        var resp = await submitterClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_AlreadyLinked_Returns400()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "DuplicateEnsembleArrangement");
        var ensemble = await CreateEnsembleAsync(client, "DuplicateEnsemble");

        await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var resp = await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_ArrangementNotFound_Returns404()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "ArrangementNotFoundEnsemble");

        var resp = await client.PostAsync($"/api/arrangements/999999/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_EnsembleNotFound_Returns400()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "EnsembleNotFoundArrangement");

        var resp = await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/999999", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_AsSubmitter_NotOwner_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(adminClient, "NotOwnerArrangement");
        var ensemble = await CreateEnsembleAsync(adminClient, "NotOwnerEnsemble");

        var submitterClient = await GetSubmitterClientAsync();
        var resp = await submitterClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_AsSubmitter_OwnerButNotMember_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "NotMemberEnsemble");

        var submitterClient = await GetSubmitterClientAsync();
        var arrangement = await CreateArrangementAsync(submitterClient, "OwnerNotMemberArrangement");

        var resp = await submitterClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_AsUser_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(adminClient, "UserRoleAddEnsembleArrangement");
        var ensemble = await CreateEnsembleAsync(adminClient, "UserRoleAddEnsemble");

        var userClient = await GetUserClientAsync();
        var resp = await userClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddEnsemble_Unauthenticated_Returns401()
    {
        var adminClient = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(adminClient, "UnauthAddEnsembleArrangement");
        var ensemble = await CreateEnsembleAsync(adminClient, "UnauthAddEnsemble");

        var anonClient = GetUnauthenticatedClient();
        var resp = await anonClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ───── RemoveEnsemble ─────

    [Fact]
    public async Task RemoveEnsemble_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "AdminRemoveArrangement");
        var ensemble = await CreateEnsembleAsync(client, "AdminRemoveEnsemble");
        await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var resp = await client.DeleteAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveEnsemble_AsLibrarian_Returns204()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "LibrarianRemoveEnsemble");
        var libClient = await GetLibrarianClientAsync();
        var arrangement = await CreateArrangementAsync(libClient, "LibrarianRemoveArrangement");
        await libClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var resp = await libClient.DeleteAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveEnsemble_AsSubmitter_OwnerAndMember_Returns204()
    {
        var adminClient = await GetAdminClientAsync();
        var submitterId = await GetSubmitterUserIdAsync(adminClient);

        var submitterClient = await GetSubmitterClientAsync();
        var arrangement = await CreateArrangementAsync(submitterClient, "SubmitterRemoveArrangement");

        // Add submitter to ensemble and link it to the arrangement
        var ensemble = await CreateEnsembleAsync(adminClient, "SubmitterRemoveEnsemble");
        await adminClient.PostAsync($"/api/ensembles/{ensemble.Id}/members/{submitterId}", null);
        await submitterClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var resp = await submitterClient.DeleteAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveEnsemble_NotLinked_Returns404()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "RemoveNotLinkedArrangement");
        var ensemble = await CreateEnsembleAsync(client, "RemoveNotLinkedEnsemble");

        var resp = await client.DeleteAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveEnsemble_ArrangementNotFound_Returns404()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "RemoveArrangementNotFoundEnsemble");

        var resp = await client.DeleteAsync($"/api/arrangements/999999/ensembles/{ensemble.Id}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveEnsemble_AsSubmitter_NotOwner_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(adminClient, "RemoveNotOwnerArrangement");
        var ensemble = await CreateEnsembleAsync(adminClient, "RemoveNotOwnerEnsemble");
        await adminClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var submitterClient = await GetSubmitterClientAsync();
        var resp = await submitterClient.DeleteAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveEnsemble_AsSubmitter_OwnerButNotMember_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "RemoveNotMemberEnsemble");

        var submitterClient = await GetSubmitterClientAsync();
        var arrangement = await CreateArrangementAsync(submitterClient, "RemoveOwnerNotMemberArrangement");
        await adminClient.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var resp = await submitterClient.DeleteAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ───── GET includes ensembles ─────

    [Fact]
    public async Task GetArrangement_AfterAddingEnsemble_IncludesEnsemble()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "GetIncludesEnsembleArrangement");
        var ensemble = await CreateEnsembleAsync(client, "GetIncludesEnsemble");

        await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var returned = await resp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.NotNull(returned?.Ensembles);
        Assert.Contains(returned.Ensembles, e => e.Id == ensemble.Id);
    }

    // ───── Ensembles on create ─────

    [Fact]
    public async Task CreateArrangement_SubmitterWithEnsembles_StartsPublic()
    {
        var adminClient = await GetAdminClientAsync();
        var submitterId = await GetSubmitterUserIdAsync(adminClient);
        var ensemble1 = await CreateEnsembleAsync(adminClient, "NoAutoLink1");
        var ensemble2 = await CreateEnsembleAsync(adminClient, "NoAutoLink2");
        await adminClient.PostAsync($"/api/ensembles/{ensemble1.Id}/members/{submitterId}", null);
        await adminClient.PostAsync($"/api/ensembles/{ensemble2.Id}/members/{submitterId}", null);

        var submitterClient = await GetSubmitterClientAsync();
        var arrangement = await CreateArrangementAsync(submitterClient, "NoAutoLinkArrangement");

        var resp = await submitterClient.GetAsync($"/api/arrangements/{arrangement.Id}");
        var returned = await resp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.NotNull(returned);
        Assert.Empty(returned.Ensembles ?? []);
    }

    [Fact]
    public async Task CreateArrangement_UserWithNoEnsembles_HasEmptyEnsembles()
    {
        var adminClient = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(adminClient, "NoEnsemblesArrangement");

        var resp = await adminClient.GetAsync($"/api/arrangements/{arrangement.Id}");
        var returned = await resp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.NotNull(returned);
        Assert.Empty(returned.Ensembles ?? []);
    }

    // ───── Cascade ─────

    [Fact]
    public async Task DeleteArrangement_RemovesEnsembleLinks()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "CascadeArrangement");
        var ensemble = await CreateEnsembleAsync(client, "CascadeArrangementEnsemble");
        await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        await client.DeleteAsync($"/api/arrangements/{arrangement.Id}");

        // Ensemble still exists
        var ensembleResp = await client.GetAsync($"/api/ensembles/{ensemble.Id}");
        Assert.Equal(HttpStatusCode.OK, ensembleResp.StatusCode);
    }

    [Fact]
    public async Task DeleteEnsemble_RemovesArrangementLinks()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "CascadeEnsembleArrangement");
        var ensemble = await CreateEnsembleAsync(client, "CascadeEnsemble");
        await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        await client.DeleteAsync($"/api/ensembles/{ensemble.Id}");

        // Arrangement still exists and has no ensembles
        var arrResp = await client.GetAsync($"/api/arrangements/{arrangement.Id}");
        var returned = await arrResp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.DoesNotContain(returned!.Ensembles ?? [], e => e.Id == ensemble.Id);
    }

    // ───── Audit logging ─────

    [Fact]
    public async Task AddEnsemble_LogsAuditEvent()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "AuditAddArrangement");
        var ensemble = await CreateEnsembleAsync(client, "AuditAddEnsemble");
        var sinceId = await GetMaxAuditEventIdAsync();

        await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.ArrangementEnsembleAdd);
        Assert.Single(events);
        Assert.Equal(AuditEventType.ArrangementEnsembleAdd, events[0].EventType);
    }

    [Fact]
    public async Task RemoveEnsemble_LogsAuditEvent()
    {
        var client = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(client, "AuditRemoveArrangement");
        var ensemble = await CreateEnsembleAsync(client, "AuditRemoveEnsemble");
        await client.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var sinceId = await GetMaxAuditEventIdAsync();

        await client.DeleteAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}");

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.ArrangementEnsembleRemove);
        Assert.Single(events);
        Assert.Equal(AuditEventType.ArrangementEnsembleRemove, events[0].EventType);
    }
}
