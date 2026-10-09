namespace GSO_Library.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(string folderPath, string storedFileName, Stream content);
    Task<Stream> GetFileAsync(string folderPath, string storedFileName);
    Task DeleteFileAsync(string folderPath, string storedFileName);
    Task<bool> ExistsAsync(string folderPath, string storedFileName);
    /// <summary>Lists every stored file at or below <paramref name="folderPrefix"/>.</summary>
    Task<IReadOnlyList<StoredFileInfo>> ListFilesAsync(string folderPrefix, CancellationToken ct = default);
}
