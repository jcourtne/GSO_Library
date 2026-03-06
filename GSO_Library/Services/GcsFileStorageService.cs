using Google.Cloud.Storage.V1;

namespace GSO_Library.Services;

public class GcsFileStorageService : IFileStorageService
{
    private readonly StorageClient _storageClient;
    private readonly string _bucketName;

    public GcsFileStorageService(IConfiguration configuration)
    {
        _bucketName = configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS:BucketName is not configured");
        // On Cloud Run, StorageClient.Create() uses the attached service account automatically.
        _storageClient = StorageClient.Create();
    }

    public async Task<string> SaveFileAsync(int arrangementId, string storedFileName, Stream content)
    {
        var objectName = $"arrangements/{arrangementId}/{storedFileName}";
        await _storageClient.UploadObjectAsync(_bucketName, objectName, null, content);
        return objectName;
    }

    public async Task<Stream> GetFileAsync(int arrangementId, string storedFileName)
    {
        var objectName = $"arrangements/{arrangementId}/{storedFileName}";
        var ms = new MemoryStream();
        await _storageClient.DownloadObjectAsync(_bucketName, objectName, ms);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteFileAsync(int arrangementId, string storedFileName)
    {
        var objectName = $"arrangements/{arrangementId}/{storedFileName}";
        try
        {
            await _storageClient.DeleteObjectAsync(_bucketName, objectName);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Treat missing objects as already deleted, matching LocalFileStorageService behavior.
        }
    }
}
