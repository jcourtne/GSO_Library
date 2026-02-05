namespace GSO_Library.Configuration;

public class FileUploadSettings
{
    public string[] AllowedExtensions { get; set; } = [];
    public long MaxFileSizeBytes { get; set; }
}
