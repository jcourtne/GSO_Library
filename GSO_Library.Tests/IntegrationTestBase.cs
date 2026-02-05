using System.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using GSO_Library.Data;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GSO_Library.Tests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    protected async Task<HttpClient> GetAuthenticatedClientAsync(string username, string password)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    protected Task<HttpClient> GetAdminClientAsync() =>
        GetAuthenticatedClientAsync("testadmin", "Admin123!");

    protected Task<HttpClient> GetEditorClientAsync() =>
        GetAuthenticatedClientAsync("testeditor", "Editor123!");

    protected Task<HttpClient> GetUserClientAsync() =>
        GetAuthenticatedClientAsync("testuser", "User1234!");

    protected HttpClient GetUnauthenticatedClient() =>
        Factory.CreateClient();

    protected static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    protected async Task<int> GetMaxAuditEventIdAsync()
    {
        using var scope = Factory.Services.CreateScope();
        using var connection = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>().CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COALESCE(MAX(id), 0) FROM audit_events");
    }

    protected async Task<List<AuditEvent>> GetAuditEventsSinceAsync(int sinceId, string? eventType = null)
    {
        using var scope = Factory.Services.CreateScope();
        using var connection = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>().CreateConnection();

        var sql = "SELECT * FROM audit_events WHERE id > @SinceId";
        if (eventType != null)
            sql += " AND event_type = @EventType";
        sql += " ORDER BY id";

        var events = await connection.QueryAsync<AuditEvent>(sql, new { SinceId = sinceId, EventType = eventType });
        return events.ToList();
    }
}
