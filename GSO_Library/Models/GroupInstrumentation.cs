namespace GSO_Library.Models;

public class GroupInstrumentation
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<Arrangement> Arrangements { get; set; } = [];
}
