namespace GSO_Library.Models;

public class Series
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Navigation properties
    public virtual ICollection<Game> Games { get; set; } = [];
}
