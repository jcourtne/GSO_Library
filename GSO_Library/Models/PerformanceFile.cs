namespace GSO_Library.Models;

public class PerformanceFile
{
    public int Id { get; set; }
    public int PerformanceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? CreatedBy { get; set; }
}
