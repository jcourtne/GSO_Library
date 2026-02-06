namespace GSO_Library.Models;

public class Ensemble
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? ContactInfo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<Performance> Performances { get; set; } = [];
}
