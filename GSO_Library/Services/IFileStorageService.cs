namespace GSO_Library.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(int arrangementId, string storedFileName, Stream content);
    Task<Stream> GetFileAsync(int arrangementId, string storedFileName);
    Task DeleteFileAsync(int arrangementId, string storedFileName);
}
