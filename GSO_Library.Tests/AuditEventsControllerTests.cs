using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class AuditEventsControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuditEventsControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAuditEvents_AsAdmin_ReturnsOk()
    {
        var client = await GetAdminClientAsync();

        var response = await client.GetAsync("/api/audit-events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var events = JsonSerializer.Deserialize<List<AuditEvent>>(body, JsonOpts);
        Assert.NotNull(events);
        Assert.NotEmpty(events);
    }

    [Fact]
    public async Task GetAuditEvents_WithEventTypeFilter_ReturnsFilteredResults()
    {
        var client = await GetAdminClientAsync();

        var response = await client.GetAsync($"/api/audit-events?eventTypes={AuditEventType.LoginSuccess}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var events = JsonSerializer.Deserialize<List<AuditEvent>>(body, JsonOpts);
        Assert.NotNull(events);
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Equal(AuditEventType.LoginSuccess, e.EventType));
    }

    [Fact]
    public async Task GetAuditEvents_WithDateRangeFilter_ExcludesOutOfRangeEvents()
    {
        var client = await GetAdminClientAsync();

        // Use a date range in the distant past — should return no events
        var from = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O");
        var to = new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc).ToString("O");

        var response = await client.GetAsync($"/api/audit-events?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var events = JsonSerializer.Deserialize<List<AuditEvent>>(body, JsonOpts);
        Assert.NotNull(events);
        Assert.Empty(events);
    }

    [Fact]
    public async Task GetAuditEvents_AsNonAdmin_ReturnsForbidden()
    {
        var client = await GetEditorClientAsync();

        var response = await client.GetAsync("/api/audit-events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
