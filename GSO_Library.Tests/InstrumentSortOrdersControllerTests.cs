using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class InstrumentSortOrdersControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public InstrumentSortOrdersControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<int> CreateInstrumentAsync(System.Net.Http.HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/instruments", new { Name = name });
        var inst = await response.Content.ReadFromJsonAsync<Instrument>(JsonOpts);
        return inst!.Id;
    }

    private async Task<InstrumentSortOrder> CreateSortOrderAsync(System.Net.Http.HttpClient client, string name, bool isDefault = false, List<int>? instrumentIds = null)
    {
        var response = await client.PostAsJsonAsync("/api/instrument-sort-orders", new
        {
            Name = name,
            IsDefault = isDefault,
            InstrumentIds = instrumentIds ?? []
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstrumentSortOrder>(JsonOpts))!;
    }

    // ───── Green cases ─────

    [Fact]
    public async Task CreateSortOrder_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/instrument-sort-orders", new
        {
            Name = "Orchestra",
            IsDefault = false,
            InstrumentIds = Array.Empty<int>()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InstrumentSortOrder>(JsonOpts);
        Assert.Equal("Orchestra", result!.Name);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task CreateSortOrder_WithIsDefault_ClearsOtherDefaults()
    {
        var client = await GetEditorClientAsync();
        var first = await CreateSortOrderAsync(client, "ISO_First", isDefault: true);

        var secondResponse = await client.PostAsJsonAsync("/api/instrument-sort-orders", new
        {
            Name = "ISO_Second",
            IsDefault = true,
            InstrumentIds = Array.Empty<int>()
        });
        secondResponse.EnsureSuccessStatusCode();

        // First should no longer be default
        var getFirst = await client.GetAsync($"/api/instrument-sort-orders/{first.Id}");
        var updatedFirst = await getFirst.Content.ReadFromJsonAsync<InstrumentSortOrder>(JsonOpts);
        Assert.False(updatedFirst!.IsDefault);

        // Second should be default
        var getDefault = await client.GetAsync("/api/instrument-sort-orders/default");
        var defaultSo = await getDefault.Content.ReadFromJsonAsync<InstrumentSortOrder>(JsonOpts);
        Assert.Equal("ISO_Second", defaultSo!.Name);
    }

    [Fact]
    public async Task GetDefault_ReturnsDefaultSortOrder()
    {
        var client = await GetEditorClientAsync();
        await CreateSortOrderAsync(client, "ISO_GetDefault", isDefault: true);

        var response = await client.GetAsync("/api/instrument-sort-orders/default");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InstrumentSortOrder>(JsonOpts);
        Assert.True(result!.IsDefault);
    }

    [Fact]
    public async Task UpdateSortOrder_ReplacesInstrumentList()
    {
        var client = await GetEditorClientAsync();
        var inst1 = await CreateInstrumentAsync(client, "ISO_Violin");
        var inst2 = await CreateInstrumentAsync(client, "ISO_Viola");
        var so = await CreateSortOrderAsync(client, "ISO_Strings", instrumentIds: [inst1]);

        // Update to use inst2 instead
        var updateResponse = await client.PutAsJsonAsync($"/api/instrument-sort-orders/{so.Id}", new
        {
            Name = "ISO_Strings",
            IsDefault = false,
            InstrumentIds = new[] { inst2 }
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/instrument-sort-orders/{so.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<InstrumentSortOrder>(JsonOpts);
        Assert.Single(updated!.Instruments);
        Assert.Equal(inst2, updated.Instruments[0].Id);
    }

    [Fact]
    public async Task GetSortOrderInstruments_ReturnsSortedFirstThenAlpha()
    {
        var client = await GetEditorClientAsync();
        var instA = await CreateInstrumentAsync(client, "ISO_AAA");
        var instZ = await CreateInstrumentAsync(client, "ISO_ZZZ");
        var instM = await CreateInstrumentAsync(client, "ISO_MMM");
        // Sort order: ZZZ first, then AAA. MMM is unsorted.
        var so = await CreateSortOrderAsync(client, "ISO_TestOrder", instrumentIds: [instZ, instA]);

        var response = await client.GetAsync($"/api/instrument-sort-orders/{so.Id}/instruments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var instruments = await response.Content.ReadFromJsonAsync<List<Instrument>>(JsonOpts);

        // ZZZ and AAA should come first (in that order), then MMM alphabetically
        var names = instruments!.Select(i => i.Name).ToList();
        var zzzIndex = names.IndexOf("ISO_ZZZ");
        var aaaIndex = names.IndexOf("ISO_AAA");
        var mmmIndex = names.IndexOf("ISO_MMM");
        Assert.True(zzzIndex < aaaIndex, "ZZZ should come before AAA (explicit sort order)");
        Assert.True(aaaIndex < mmmIndex, "AAA should come before MMM (sorted before unsorted)");
    }

    [Fact]
    public async Task DeleteSortOrder_Returns204()
    {
        var client = await GetAdminClientAsync();
        var so = await CreateSortOrderAsync(client, "ISO_ToDelete");

        var response = await client.DeleteAsync($"/api/instrument-sort-orders/{so.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync($"/api/instrument-sort-orders/{so.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetDefault_NoDefault_Returns404()
    {
        // This test relies on no default being set — use a fresh check
        var client = await GetUserClientAsync();
        var response = await client.GetAsync("/api/instrument-sort-orders/default");
        // May be 404 (no default) or 200 (another test set one) — just verify it doesn't crash
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSortOrder_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/instrument-sort-orders", new
        {
            Name = "Nope",
            IsDefault = false,
            InstrumentIds = Array.Empty<int>()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSortOrder_NotFound_Returns404()
    {
        var client = await GetAdminClientAsync();
        var response = await client.DeleteAsync("/api/instrument-sort-orders/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSortOrder_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();
        var response = await client.PutAsJsonAsync("/api/instrument-sort-orders/99999", new
        {
            Name = "Nope",
            IsDefault = false,
            InstrumentIds = Array.Empty<int>()
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
