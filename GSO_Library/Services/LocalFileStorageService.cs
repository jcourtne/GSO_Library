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

    public Task<bool> ExistsAsync(string folderPath, string storedFileName)
    {
        var filePath = Path.Combine(_basePath, folderPath, storedFileName);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task DeleteFileAsync(string folderPath, string storedFileName)
    {
        var filePath = Path.Combine(_basePath, folderPath, storedFileName);

        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredFileInfo>> ListFilesAsync(string folderPrefix, CancellationToken ct = default)
    {
        var root = Path.Combine(_basePath, folderPrefix);
        if (!Directory.Exists(root))
            return Task.FromResult<IReadOnlyList<StoredFileInfo>>([]);

        var files = new List<StoredFileInfo>();
        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var folderPath = Path.GetRelativePath(_basePath, Path.GetDirectoryName(filePath)!).Replace('\\', '/');
            files.Add(new StoredFileInfo(folderPath, Path.GetFileName(filePath),
                new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero)));
        }
        return Task.FromResult<IReadOnlyList<StoredFileInfo>>(files);
    }
}
