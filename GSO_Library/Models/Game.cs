namespace GSO_Library.Models;

public class Game
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SeriesId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation properties
    public virtual Series? Series { get; set; }
    public virtual ICollection<Arrangement> Arrangements { get; set; } = [];
}
