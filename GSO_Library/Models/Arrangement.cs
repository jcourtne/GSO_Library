namespace GSO_Library.Models;

public class Arrangement
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ArrangementFilePath { get; set; }
    public string? PdfFilePath { get; set; }
    
    // Navigation properties
    public virtual ICollection<Game> Games { get; set; } = [];
    public virtual ICollection<Performance> Performances { get; set; } = [];
}
