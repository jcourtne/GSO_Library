namespace GSO_Library.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(string folderPath, string storedFileName, Stream content);
    Task<Stream> GetFileAsync(string folderPath, string storedFileName);
    Task DeleteFileAsync(string folderPath, string storedFileName);
}
