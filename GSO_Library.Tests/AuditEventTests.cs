using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class AuditEventTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuditEventTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Login_Success_CreatesAuditEvent()
    {
        var sinceId = await GetMaxAuditEventIdAsync();
        var client = GetUnauthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "testadmin",
            Password = "Admin123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.LoginSuccess);
        Assert.Contains(events, e => e.Username == "testadmin");
    }

    [Fact]
    public async Task Login_UnknownUser_CreatesFailureEvent()
    {
        var sinceId = await GetMaxAuditEventIdAsync();
        var client = GetUnauthenticatedClient();

        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "nonexistent_user",
            Password = "Whatever123!"
        });

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.LoginFailure);
        var evt = Assert.Single(events, e => e.Username == "nonexistent_user");
        Assert.Contains("unknown_user", evt.Detail);
    }

    [Fact]
    public async Task Login_WrongPassword_CreatesFailureEvent()
    {
        var sinceId = await GetMaxAuditEventIdAsync();
        var client = GetUnauthenticatedClient();

        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "testadmin",
            Password = "WrongPassword1!"
        });

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.LoginFailure);
        var evt = Assert.Single(events, e => e.Username == "testadmin");
        Assert.Contains("wrong_password", evt.Detail);
    }

    [Fact]
    public async Task Login_DisabledAccount_CreatesFailureEvent()
    {
        // Disable testuser, then try to log in as them
        var adminClient = await GetAdminClientAsync();
        var usersResponse = await adminClient.GetAsync("/api/auth/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        var targetUser = users!.First(u => u.UserName == "testuser");

        await adminClient.PostAsync($"/api/auth/disable/{targetUser.Id}", null);

        var sinceId = await GetMaxAuditEventIdAsync();
        var client = GetUnauthenticatedClient();

        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "testuser",
            Password = "User1234!"
        });

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.LoginFailure);
        var evt = Assert.Single(events, e => e.Username == "testuser");
        Assert.Contains("disabled_account", evt.Detail);

        // Re-enable user for other tests
        await adminClient.PostAsync($"/api/auth/enable/{targetUser.Id}", null);
    }

    [Fact]
    public async Task TokenRefresh_CreatesAuditEvent()
    {
        var client = GetUnauthenticatedClient();

        // Login first
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "testeditor",
            Password = "Editor123!"
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

        var sinceId = await GetMaxAuditEventIdAsync();

        // Refresh
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest
        {
            RefreshToken = loginBody!.RefreshToken!
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.TokenRefresh);
        Assert.Contains(events, e => e.Username == "testeditor");
    }

    [Fact]
    public async Task DisableAccount_CreatesAuditEvent()
    {
        var adminClient = await GetAdminClientAsync();
        var usersResponse = await adminClient.GetAsync("/api/auth/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        var target = users!.First(u => u.UserName == "testuser");

        var sinceId = await GetMaxAuditEventIdAsync();

        await adminClient.PostAsync($"/api/auth/disable/{target.Id}", null);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.AccountDisable);
        var evt = Assert.Single(events);
        Assert.Equal("testadmin", evt.Username);
        Assert.Equal("testuser", evt.TargetUsername);

        // Re-enable for other tests
        await adminClient.PostAsync($"/api/auth/enable/{target.Id}", null);
    }

    [Fact]
    public async Task EnableAccount_CreatesAuditEvent()
    {
        var adminClient = await GetAdminClientAsync();
        var usersResponse = await adminClient.GetAsync("/api/auth/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        var target = users!.First(u => u.UserName == "testuser");

        // Disable first
        await adminClient.PostAsync($"/api/auth/disable/{target.Id}", null);

        var sinceId = await GetMaxAuditEventIdAsync();

        await adminClient.PostAsync($"/api/auth/enable/{target.Id}", null);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.AccountEnable);
        var evt = Assert.Single(events);
        Assert.Equal("testadmin", evt.Username);
        Assert.Equal("testuser", evt.TargetUsername);
    }

    [Fact]
    public async Task GrantRole_CreatesAuditEvent()
    {
        var adminClient = await GetAdminClientAsync();
        var usersResponse = await adminClient.GetAsync("/api/auth/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        var target = users!.First(u => u.UserName == "testuser");

        var sinceId = await GetMaxAuditEventIdAsync();

        var response = await adminClient.PostAsJsonAsync("/api/auth/grant-role", new RoleManagementRequest
        {
            UserId = target.Id,
            Role = "Editor"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.RoleGrant);
        var evt = Assert.Single(events);
        Assert.Equal("testadmin", evt.Username);
        Assert.Equal("testuser", evt.TargetUsername);
        Assert.Contains("Editor", evt.Detail);

        // Remove the role for other tests
        await adminClient.PostAsJsonAsync("/api/auth/remove-role", new RoleManagementRequest
        {
            UserId = target.Id,
            Role = "Editor"
        });
    }

    [Fact]
    public async Task RemoveRole_CreatesAuditEvent()
    {
        var adminClient = await GetAdminClientAsync();
        var usersResponse = await adminClient.GetAsync("/api/auth/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        var target = users!.First(u => u.UserName == "testuser");

        // Grant first
        await adminClient.PostAsJsonAsync("/api/auth/grant-role", new RoleManagementRequest
        {
            UserId = target.Id,
            Role = "Editor"
        });

        var sinceId = await GetMaxAuditEventIdAsync();

        var response = await adminClient.PostAsJsonAsync("/api/auth/remove-role", new RoleManagementRequest
        {
            UserId = target.Id,
            Role = "Editor"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.RoleRemove);
        var evt = Assert.Single(events);
        Assert.Equal("testadmin", evt.Username);
        Assert.Equal("testuser", evt.TargetUsername);
        Assert.Contains("Editor", evt.Detail);
    }

    [Fact]
    public async Task FileDownload_CreatesAuditEvent()
    {
        var client = await GetEditorClientAsync();

        // Create arrangement
        var arrResponse = await client.PostAsJsonAsync("/api/arrangements", new ArrangementRequest
        {
            Name = "Audit Download Test",
            Composer = "Test Composer"
        });
        var arrangement = await arrResponse.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);

        // Upload file
        var content = new MultipartFormDataContent();
        var fileBytes = "audit test file content"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "audit-test.pdf");
        var uploadResponse = await client.PostAsync($"/api/arrangements/{arrangement!.Id}/files", content);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadedFile = await uploadResponse.Content.ReadFromJsonAsync<ArrangementFile>(JsonOpts);

        var sinceId = await GetMaxAuditEventIdAsync();

        // Download file
        var downloadResponse = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{uploadedFile!.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        var events = await GetAuditEventsSinceAsync(sinceId, AuditEventType.FileDownload);
        var evt = Assert.Single(events);
        Assert.Equal("testeditor", evt.Username);
        Assert.Contains($"arrangementId: {arrangement.Id}", evt.Detail);
        Assert.Contains($"fileId: {uploadedFile.Id}", evt.Detail);
        Assert.Contains("audit-test.pdf", evt.Detail);
    }
}
