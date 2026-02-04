namespace GSO_Library.Dtos;

public class ArrangementRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Arranger { get; set; }
    public string? Composer { get; set; }
    public string? Key { get; set; }
    public int? DurationSeconds { get; set; }
    public int? Year { get; set; }
}
