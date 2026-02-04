namespace GSO_Library.Models;

public class ArrangementFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }

    // Foreign key
    public int ArrangementId { get; set; }

    // Navigation property
    public virtual Arrangement Arrangement { get; set; } = null!;
}
