namespace GSO_Library.Models;

public class Performance
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime? PerformanceDate { get; set; }
    public string? Notes { get; set; }
    public int ArrangementId { get; set; }
    
    // Navigation properties
    public virtual Arrangement? Arrangement { get; set; }
}
