using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class PerformancesControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PerformancesControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ───── Green cases ─────

    [Fact]
    public async Task CreatePerformance_AsEditor_Returns201()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Spring Concert 2025",
            Link = "https://example.com/concert",
            PerformanceDate = "2025-04-15T19:00:00Z",
            Notes = "Opening night"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var perf = await response.Content.ReadFromJsonAsync<Performance>(JsonOpts);
        Assert.Equal("Spring Concert 2025", perf!.Name);
        Assert.True(perf.Id > 0);
    }

    [Fact]
    public async Task GetAllPerformances_Authenticated_Returns200()
    {
        var client = await GetEditorClientAsync();
        await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Fall Concert",
            Link = "https://example.com/fall"
        });

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync("/api/performances");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Performance>>(JsonOpts);
        Assert.NotNull(result);
        Assert.True(result!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetPerformanceById_Exists_Returns200()
    {
        var client = await GetEditorClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Winter Gala",
            Link = "https://example.com/gala"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Performance>(JsonOpts);

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/performances/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var perf = await response.Content.ReadFromJsonAsync<Performance>(JsonOpts);
        Assert.Equal("Winter Gala", perf!.Name);
    }

    [Fact]
    public async Task UpdatePerformance_AsEditor_Returns200()
    {
        var client = await GetEditorClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "OldPerf",
            Link = "https://example.com/old"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Performance>(JsonOpts);

        var response = await client.PutAsJsonAsync($"/api/performances/{created!.Id}", new
        {
            Name = "Updated Performance",
            Link = "https://example.com/updated",
            Notes = "Updated notes"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Performance>(JsonOpts);
        Assert.Equal("Updated Performance", updated!.Name);
    }

    [Fact]
    public async Task DeletePerformance_AsAdmin_Returns204()
    {
        var client = await GetAdminClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "ToDelete",
            Link = "https://example.com/delete"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<Performance>(JsonOpts);

        var response = await client.DeleteAsync($"/api/performances/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify gone
        var getResponse = await client.GetAsync($"/api/performances/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ───── Sorting ─────

    [Fact]
    public async Task GetAllPerformances_SortByNameDesc_ReturnsSorted()
    {
        var client = await GetEditorClientAsync();
        await client.PostAsJsonAsync("/api/performances", new { Name = "AAA_PerfSort", Link = "https://example.com/a" });
        await client.PostAsJsonAsync("/api/performances", new { Name = "ZZZ_PerfSort", Link = "https://example.com/z" });

        var response = await client.GetAsync("/api/performances?sortBy=name&sortDirection=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Performance>>(JsonOpts);
        var names = result!.Items.Select(p => p.Name).ToList();
        Assert.Equal(names.OrderByDescending(n => n).ToList(), names);
    }

    // ───── Audit fields ─────

    [Fact]
    public async Task CreatePerformance_SetsAuditFields()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "AuditPerf",
            Link = "https://example.com/audit"
        });
        response.EnsureSuccessStatusCode();
        var perf = await response.Content.ReadFromJsonAsync<Performance>(JsonOpts);

        Assert.NotEqual(default, perf!.CreatedAt);
        Assert.NotEqual(default, perf.UpdatedAt);
        Assert.Equal("testeditor", perf.CreatedBy);
    }

    // ───── Error cases ─────

    [Fact]
    public async Task GetPerformanceById_NotFound_Returns404()
    {
        var client = await GetUserClientAsync();

        var response = await client.GetAsync("/api/performances/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePerformance_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.PutAsJsonAsync("/api/performances/99999", new
        {
            Name = "Nope",
            Link = "https://example.com"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletePerformance_NotFound_Returns404()
    {
        var client = await GetEditorClientAsync();

        var response = await client.DeleteAsync("/api/performances/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatePerformance_Unauthenticated_Returns401()
    {
        var client = GetUnauthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Nope",
            Link = "https://example.com"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePerformance_AsRegularUser_Returns403()
    {
        var client = await GetUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = "Nope",
            Link = "https://example.com"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ───── Performance files ─────

    private async Task<Performance> CreatePerformanceAsync(HttpClient client, string name = "FilesTest Concert")
    {
        var response = await client.PostAsJsonAsync("/api/performances", new
        {
            Name = name,
            Link = "https://example.com/files-test"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Performance>(JsonOpts))!;
    }

    private static MultipartFormDataContent CreatePdfContent(string fileName = "program.pdf")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task UploadFile_AsAdmin_ReturnsPdfMetadata()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Upload_Admin");

        var response = await adminClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var file = await response.Content.ReadFromJsonAsync<PerformanceFile>(JsonOpts);
        Assert.NotNull(file);
        Assert.Equal("program.pdf", file!.FileName);
        Assert.Equal(perf.Id, file.PerformanceId);
    }

    [Fact]
    public async Task UploadFile_AsEditor_Returns201()
    {
        var editorClient = await GetEditorClientAsync();
        var perf = await CreatePerformanceAsync(editorClient, "PF_Upload_Editor");

        var response = await editorClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_NonPdf_Returns400()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Upload_NonPdf");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "notes.txt");

        var response = await adminClient.PostAsync($"/api/performances/{perf.Id}/files", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_AsUser_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Upload_UserForbid");

        var userClient = await GetUserClientAsync();
        var response = await userClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListFiles_AsUser_Returns200()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_List_User");
        await adminClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/performances/{perf.Id}/files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await response.Content.ReadFromJsonAsync<List<PerformanceFile>>(JsonOpts);
        Assert.Single(files!);
    }

    [Fact]
    public async Task DownloadFile_AsUser_Returns200()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Download_User");
        var uploadResp = await adminClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());
        var file = await uploadResp.Content.ReadFromJsonAsync<PerformanceFile>(JsonOpts);

        var userClient = await GetUserClientAsync();
        var response = await userClient.GetAsync($"/api/performances/{perf.Id}/files/{file!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_Unauthenticated_Returns401()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Download_Unauth");
        var uploadResp = await adminClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());
        var file = await uploadResp.Content.ReadFromJsonAsync<PerformanceFile>(JsonOpts);

        var anonClient = GetUnauthenticatedClient();
        var response = await anonClient.GetAsync($"/api/performances/{perf.Id}/files/{file!.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_LogsAuditEvent()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Audit_Download");
        var uploadResp = await adminClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());
        var file = await uploadResp.Content.ReadFromJsonAsync<PerformanceFile>(JsonOpts);

        var sinceId = await GetMaxAuditEventIdAsync();
        await adminClient.GetAsync($"/api/performances/{perf.Id}/files/{file!.Id}");

        var events = await GetAuditEventsSinceAsync(sinceId, "FileDownload");
        Assert.Single(events);
        Assert.Contains($"performanceId: {perf.Id}", events[0].Detail);
    }

    [Fact]
    public async Task DeleteFile_AsAdmin_Returns204()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Delete_Admin");
        var uploadResp = await adminClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());
        var file = await uploadResp.Content.ReadFromJsonAsync<PerformanceFile>(JsonOpts);

        var response = await adminClient.DeleteAsync($"/api/performances/{perf.Id}/files/{file!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify gone
        var listResp = await adminClient.GetAsync($"/api/performances/{perf.Id}/files");
        var files = await listResp.Content.ReadFromJsonAsync<List<PerformanceFile>>(JsonOpts);
        Assert.Empty(files!);
    }

    [Fact]
    public async Task DeleteFile_AsUser_Returns403()
    {
        var adminClient = await GetAdminClientAsync();
        var perf = await CreatePerformanceAsync(adminClient, "PF_Delete_UserForbid");
        var uploadResp = await adminClient.PostAsync($"/api/performances/{perf.Id}/files", CreatePdfContent());
        var file = await uploadResp.Content.ReadFromJsonAsync<PerformanceFile>(JsonOpts);

        var userClient = await GetUserClientAsync();
        var response = await userClient.DeleteAsync($"/api/performances/{perf.Id}/files/{file!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
