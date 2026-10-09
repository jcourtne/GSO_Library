namespace GSO_Library.Services;

/// <summary>
/// A stored file as returned by <see cref="IFileStorageService.ListFilesAsync"/>, split into the same
/// folder/name pair the other storage methods take.
/// </summary>
public record StoredFileInfo(string FolderPath, string StoredFileName, DateTimeOffset LastModified);
