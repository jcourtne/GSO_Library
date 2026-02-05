using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
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
}
