namespace GSO_Library.Models;

public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EnsembleId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public string? ShareToken { get; set; }
    public bool ShareIncludePdf { get; set; } = true;
    public bool ShareIncludeNotation { get; set; }
    public bool ShareIncludePlayback { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SharePasswordHash { get; set; }
    public bool HasSharePassword => SharePasswordHash != null;

    // Navigation properties
    public virtual Ensemble? Ensemble { get; set; }
    public virtual ICollection<Arrangement> Arrangements { get; set; } = [];
    public virtual ICollection<Performance> Performances { get; set; } = [];
    public virtual SeasonAnalytics? Analytics { get; set; }
}
