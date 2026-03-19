namespace GSO_Library.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _basePath = configuration.GetSection("FileStorage")["BasePath"]
            ?? throw new InvalidOperationException("FileStorage:BasePath is not configured");
    }

    public async Task<string> SaveFileAsync(string folderPath, string storedFileName, Stream content)
    {
        var directory = Path.Combine(_basePath, folderPath);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, storedFileName);
        using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream);

        return filePath;
    }

    public Task<Stream> GetFileAsync(string folderPath, string storedFileName)
    {
        var filePath = Path.Combine(_basePath, folderPath, storedFileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found on disk.", filePath);

        Stream stream = File.OpenRead(filePath);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string folderPath, string storedFileName)
    {
        var filePath = Path.Combine(_basePath, folderPath, storedFileName);

        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }
}
