using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class EnsembleMembersControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public EnsembleMembersControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Ensemble> CreateEnsembleAsync(HttpClient client, string name = "MemberTestEnsemble")
    {
        var resp = await client.PostAsJsonAsync("/api/ensembles", new { Name = name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Ensemble>(JsonOpts))!;
    }

    private async Task<string> GetTestUserIdAsync(HttpClient adminClient)
    {
        var resp = await adminClient.GetAsync("/api/auth/users");
        resp.EnsureSuccessStatusCode();
        var users = await resp.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        return users!.First(u => u.UserName == "testuser").Id;
    }

    // ───── AddMember ─────

    [Fact]
    public async Task AddMember_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "AddMemberEnsemble");
        var userId = await GetTestUserIdAsync(client);

        var resp = await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task AddMember_AlreadyLinked_Returns409()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "DuplicateMemberEnsemble");
        var userId = await GetTestUserIdAsync(client);

        await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);
        var resp = await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task AddMember_EnsembleNotFound_Returns404()
    {
        var client = await GetAdminClientAsync();
        var userId = await GetTestUserIdAsync(client);

        var resp = await client.PostAsync($"/api/ensembles/999999/members/{userId}", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task AddMember_UserNotFound_Returns404()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "UserNotFoundEnsemble");

        var resp = await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/nonexistent-user-id", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task AddMember_AsNonAdmin_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "NonAdminAddEnsemble");
        var userId = await GetTestUserIdAsync(adminClient);

        var userClient = await GetUserClientAsync();
        var resp = await userClient.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddMember_Unauthenticated_Returns401()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "UnauthAddEnsemble");
        var userId = await GetTestUserIdAsync(adminClient);

        var anonClient = GetUnauthenticatedClient();
        var resp = await anonClient.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ───── RemoveMember ─────

    [Fact]
    public async Task RemoveMember_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "RemoveMemberEnsemble");
        var userId = await GetTestUserIdAsync(client);

        await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);
        var resp = await client.DeleteAsync($"/api/ensembles/{ensemble.Id}/members/{userId}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_NotLinked_Returns404()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "RemoveNotLinkedEnsemble");
        var userId = await GetTestUserIdAsync(client);

        var resp = await client.DeleteAsync($"/api/ensembles/{ensemble.Id}/members/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_EnsembleNotFound_Returns404()
    {
        var client = await GetAdminClientAsync();
        var userId = await GetTestUserIdAsync(client);

        var resp = await client.DeleteAsync($"/api/ensembles/999999/members/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ───── GetMembers ─────

    [Fact]
    public async Task GetMembers_AsAdmin_Returns200WithLinkedUser()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "GetMembersEnsemble");
        var userId = await GetTestUserIdAsync(client);

        await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);
        var resp = await client.GetAsync($"/api/ensembles/{ensemble.Id}/members");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var members = await resp.Content.ReadFromJsonAsync<List<EnsembleMemberDto>>(JsonOpts);
        Assert.NotNull(members);
        Assert.Contains(members, m => m.Id == userId);
    }

    [Fact]
    public async Task GetMembers_EmptyEnsemble_Returns200EmptyArray()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "EmptyMembersEnsemble");

        var resp = await client.GetAsync($"/api/ensembles/{ensemble.Id}/members");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var members = await resp.Content.ReadFromJsonAsync<List<EnsembleMemberDto>>(JsonOpts);
        Assert.NotNull(members);
        Assert.Empty(members);
    }

    [Fact]
    public async Task GetMembers_AsNonAdmin_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "NonAdminGetMembersEnsemble");

        var userClient = await GetUserClientAsync();
        var resp = await userClient.GetAsync($"/api/ensembles/{ensemble.Id}/members");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetMembers_Unauthenticated_Returns401()
    {
        var adminClient = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(adminClient, "UnauthGetMembersEnsemble");

        var anonClient = GetUnauthenticatedClient();
        var resp = await anonClient.GetAsync($"/api/ensembles/{ensemble.Id}/members");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ───── GetUserEnsembles ─────

    [Fact]
    public async Task GetUserEnsembles_AsAdmin_Returns200WithEnsemble()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "GetUserEnsemblesEnsemble");
        var userId = await GetTestUserIdAsync(client);

        await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);
        var resp = await client.GetAsync($"/api/auth/users/{userId}/ensembles");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var ensembles = await resp.Content.ReadFromJsonAsync<List<Ensemble>>(JsonOpts);
        Assert.NotNull(ensembles);
        Assert.Contains(ensembles, e => e.Id == ensemble.Id);
    }

    [Fact]
    public async Task GetUserEnsembles_AsNonAdmin_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var userId = await GetTestUserIdAsync(adminClient);

        var userClient = await GetUserClientAsync();
        var resp = await userClient.GetAsync($"/api/auth/users/{userId}/ensembles");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetUserEnsembles_UserNotFound_Returns404()
    {
        var client = await GetAdminClientAsync();

        var resp = await client.GetAsync("/api/auth/users/nonexistent-user-id/ensembles");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ───── Cascade / data integrity ─────

    [Fact]
    public async Task DeleteEnsemble_RemovesMemberships()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "CascadeEnsemble");
        var userId = await GetTestUserIdAsync(client);

        await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);
        await client.DeleteAsync($"/api/ensembles/{ensemble.Id}");

        // Ensemble is gone; user should still exist
        var userResp = await client.GetAsync($"/api/auth/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, userResp.StatusCode);

        // No ensembles for user
        var ensemblesResp = await client.GetAsync($"/api/auth/users/{userId}/ensembles");
        var ensembles = await ensemblesResp.Content.ReadFromJsonAsync<List<Ensemble>>(JsonOpts);
        Assert.DoesNotContain(ensembles!, e => e.Id == ensemble.Id);
    }

    // ───── Audit logging ─────

    [Fact]
    public async Task AddMember_LogsAuditEvent()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "AuditAddEnsemble");
        var userId = await GetTestUserIdAsync(client);
        var sinceId = await GetMaxAuditEventIdAsync();

        await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.UserEnsembleAdd);
        Assert.Single(events);
        Assert.Equal(AuditEventType.UserEnsembleAdd, events[0].EventType);
    }

    [Fact]
    public async Task RemoveMember_LogsAuditEvent()
    {
        var client = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(client, "AuditRemoveEnsemble");
        var userId = await GetTestUserIdAsync(client);
        await client.PostAsync($"/api/ensembles/{ensemble.Id}/members/{userId}", null);
        var sinceId = await GetMaxAuditEventIdAsync();

        await client.DeleteAsync($"/api/ensembles/{ensemble.Id}/members/{userId}");

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.UserEnsembleRemove);
        Assert.Single(events);
        Assert.Equal(AuditEventType.UserEnsembleRemove, events[0].EventType);
    }
}
