namespace GSO_Library.Models;

public class SeasonShareZip
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string ZipKey { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
