using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Xunit;

namespace GSO_Library.Tests;

public class EnsembleRoleTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public EnsembleRoleTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Arrangement> CreateArrangementAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/arrangements", new { Name = name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts))!;
    }

    private async Task<Ensemble> CreateEnsembleAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/ensembles", new { Name = name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Ensemble>(JsonOpts))!;
    }

    private async Task<string> GetUserIdAsync(HttpClient adminClient, string username)
    {
        var resp = await adminClient.GetAsync("/api/auth/users");
        resp.EnsureSuccessStatusCode();
        var users = await resp.Content.ReadFromJsonAsync<List<UserResponse>>(JsonOpts);
        return users!.First(u => u.UserName == username).Id;
    }

    private async Task<ArrangementFile> UploadFileAsync(HttpClient client, int arrangementId, string fileName, string contentType, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        var resp = await client.PostAsync($"/api/arrangements/{arrangementId}/files", content);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ArrangementFile>(JsonOpts))!;
    }

    // ───── Ensemble Downloader — visibility ─────

    [Fact]
    public async Task EnsembleDownloader_GetAllArrangements_Returns200()
    {
        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.GetAsync("/api/arrangements");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleDownloader_GetArrangementById_Returns200()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsDlGetById");

        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ───── Ensemble Downloader — downloads ─────

    [Fact]
    public async Task EnsembleDownloader_PlaybackFile_AnyArrangement_Returns200()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsDlPlayback");
        var file = await UploadFileAsync(admin, arrangement.Id, "track.mp3", "audio/mpeg", "audio"u8.ToArray());

        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleDownloader_PdfFile_PublicArrangement_Returns200()
    {
        // An arrangement with no ensemble is public — any ensemble-download role can get all its files.
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsDlPdfPublic");
        var file = await UploadFileAsync(admin, arrangement.Id, "score.pdf", "application/pdf", "pdf"u8.ToArray());

        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleDownloader_PdfFile_ArrangementInOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(admin, "EnsDlPdfOtherEnsemble");
        var arrangement = await CreateArrangementAsync(admin, "EnsDlPdfOther");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var file = await UploadFileAsync(admin, arrangement.Id, "score.pdf", "application/pdf", "pdf"u8.ToArray());

        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleDownloader_PdfFile_ArrangementInEnsemble_Returns200()
    {
        var admin = await GetAdminClientAsync();
        var ensDlId = await GetUserIdAsync(admin, "testensembledownloader");
        var ensemble = await CreateEnsembleAsync(admin, "EnsDlPdfEnsemble");
        await admin.PostAsync($"/api/ensembles/{ensemble.Id}/members/{ensDlId}", null);

        var arrangement = await CreateArrangementAsync(admin, "EnsDlPdfInEnsemble");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var file = await UploadFileAsync(admin, arrangement.Id, "score.pdf", "application/pdf", "pdf"u8.ToArray());

        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleDownloader_NotationFile_ArrangementInOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(admin, "EnsDlMxlOtherEnsemble");
        var arrangement = await CreateArrangementAsync(admin, "EnsDlMxlOther");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var file = await UploadFileAsync(admin, arrangement.Id, "score.mxl", "application/vnd.recordare.musicxml", "xml"u8.ToArray());

        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ───── Ensemble Downloader — no write access ─────

    [Fact]
    public async Task EnsembleDownloader_CreateArrangement_Returns403()
    {
        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.PostAsJsonAsync("/api/arrangements", new { Name = "ShouldFail" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleDownloader_UpdateArrangement_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsDlUpdateTarget");

        var client = await GetEnsembleDownloaderClientAsync();
        var resp = await client.PutAsJsonAsync($"/api/arrangements/{arrangement.Id}/details", new { Name = "Changed" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ───── Ensemble Librarian — visibility ─────

    [Fact]
    public async Task EnsembleLibrarian_GetAllArrangements_Returns200()
    {
        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.GetAsync("/api/arrangements");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ───── Ensemble Librarian — downloads ─────

    [Fact]
    public async Task EnsembleLibrarian_PlaybackFile_AnyArrangement_Returns200()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsLibPlayback");
        var file = await UploadFileAsync(admin, arrangement.Id, "track.mp3", "audio/mpeg", "audio"u8.ToArray());

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_PdfFile_PublicArrangement_Returns200()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsLibPdfPublic");
        var file = await UploadFileAsync(admin, arrangement.Id, "score.pdf", "application/pdf", "pdf"u8.ToArray());

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_PdfFile_ArrangementInOtherEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var ensemble = await CreateEnsembleAsync(admin, "EnsLibPdfOtherEnsemble");
        var arrangement = await CreateArrangementAsync(admin, "EnsLibPdfOther");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var file = await UploadFileAsync(admin, arrangement.Id, "score.pdf", "application/pdf", "pdf"u8.ToArray());

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_PdfFile_ArrangementInEnsemble_Returns200()
    {
        var admin = await GetAdminClientAsync();
        var ensLibId = await GetUserIdAsync(admin, "testensemblelibrarian");
        var ensemble = await CreateEnsembleAsync(admin, "EnsLibPdfEnsemble");
        await admin.PostAsync($"/api/ensembles/{ensemble.Id}/members/{ensLibId}", null);

        var arrangement = await CreateArrangementAsync(admin, "EnsLibPdfInEnsemble");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);
        var file = await UploadFileAsync(admin, arrangement.Id, "score.pdf", "application/pdf", "pdf"u8.ToArray());

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.GetAsync($"/api/arrangements/{arrangement.Id}/files/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ───── Ensemble Librarian — write access scoped to ensemble ─────

    [Fact]
    public async Task EnsembleLibrarian_CreateArrangement_StartsPublic_Returns201()
    {
        var admin = await GetAdminClientAsync();
        var ensLibId = await GetUserIdAsync(admin, "testensemblelibrarian");
        var ensemble = await CreateEnsembleAsync(admin, "EnsLibCreateEnsemble");
        await admin.PostAsync($"/api/ensembles/{ensemble.Id}/members/{ensLibId}", null);

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.PostAsJsonAsync("/api/arrangements", new { Name = "EnsLibCreated" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // Creation no longer auto-links the creator's ensembles; a new arrangement is public.
        var arrangement = (await resp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts))!;
        var getResp = await client.GetAsync($"/api/arrangements/{arrangement.Id}");
        var returned = await getResp.Content.ReadFromJsonAsync<Arrangement>(JsonOpts);
        Assert.Empty(returned!.Ensembles ?? []);
    }

    [Fact]
    public async Task EnsembleLibrarian_UpdateArrangement_InEnsemble_Returns200()
    {
        var admin = await GetAdminClientAsync();
        var ensLibId = await GetUserIdAsync(admin, "testensemblelibrarian");
        var ensemble = await CreateEnsembleAsync(admin, "EnsLibUpdateEnsemble");
        await admin.PostAsync($"/api/ensembles/{ensemble.Id}/members/{ensLibId}", null);

        var arrangement = await CreateArrangementAsync(admin, "EnsLibUpdateTarget");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.PutAsJsonAsync($"/api/arrangements/{arrangement.Id}/details", new { Name = "EnsLibUpdated" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_UpdateArrangement_NotInEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsLibUpdateNoAccess");

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.PutAsJsonAsync($"/api/arrangements/{arrangement.Id}/details", new { Name = "ShouldFail" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_DeleteArrangement_InEnsemble_Returns204()
    {
        var admin = await GetAdminClientAsync();
        var ensLibId = await GetUserIdAsync(admin, "testensemblelibrarian");
        var ensemble = await CreateEnsembleAsync(admin, "EnsLibDeleteEnsemble");
        await admin.PostAsync($"/api/ensembles/{ensemble.Id}/members/{ensLibId}", null);

        var arrangement = await CreateArrangementAsync(admin, "EnsLibDeleteTarget");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.DeleteAsync($"/api/arrangements/{arrangement.Id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_DeleteArrangement_NotInEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsLibDeleteNoAccess");

        var client = await GetEnsembleLibrarianClientAsync();
        var resp = await client.DeleteAsync($"/api/arrangements/{arrangement.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_UploadFile_InEnsemble_Returns201()
    {
        var admin = await GetAdminClientAsync();
        var ensLibId = await GetUserIdAsync(admin, "testensemblelibrarian");
        var ensemble = await CreateEnsembleAsync(admin, "EnsLibUploadEnsemble");
        await admin.PostAsync($"/api/ensembles/{ensemble.Id}/members/{ensLibId}", null);

        var arrangement = await CreateArrangementAsync(admin, "EnsLibUploadTarget");
        await admin.PostAsync($"/api/arrangements/{arrangement.Id}/ensembles/{ensemble.Id}", null);

        var client = await GetEnsembleLibrarianClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("audio"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        content.Add(fileContent, "file", "track.mp3");
        var resp = await client.PostAsync($"/api/arrangements/{arrangement.Id}/files", content);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task EnsembleLibrarian_UploadFile_NotInEnsemble_Returns403()
    {
        var admin = await GetAdminClientAsync();
        var arrangement = await CreateArrangementAsync(admin, "EnsLibUploadNoAccess");

        var client = await GetEnsembleLibrarianClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("audio"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        content.Add(fileContent, "file", "track.mp3");
        var resp = await client.PostAsync($"/api/arrangements/{arrangement.Id}/files", content);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
