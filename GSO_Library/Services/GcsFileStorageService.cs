using System.IO.Pipelines;
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

    public async Task<string> SaveFileAsync(string folderPath, string storedFileName, Stream content)
    {
        var objectName = $"{folderPath}/{storedFileName}";
        await _storageClient.UploadObjectAsync(_bucketName, objectName, null, content);
        return objectName;
    }

    public Task<Stream> GetFileAsync(string folderPath, string storedFileName)
    {
        var objectName = $"{folderPath}/{storedFileName}";
        var pipe = new Pipe();
        _ = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                await _storageClient.DownloadObjectAsync(_bucketName, objectName, pipe.Writer.AsStream());
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                await pipe.Writer.CompleteAsync(error);
            }
        });
        return Task.FromResult<Stream>(pipe.Reader.AsStream());
    }

    public async Task<bool> ExistsAsync(string folderPath, string storedFileName)
    {
        var objectName = $"{folderPath}/{storedFileName}";
        try
        {
            // Metadata-only lookup — does not download the object's content.
            await _storageClient.GetObjectAsync(_bucketName, objectName);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteFileAsync(string folderPath, string storedFileName)
    {
        var objectName = $"{folderPath}/{storedFileName}";
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
