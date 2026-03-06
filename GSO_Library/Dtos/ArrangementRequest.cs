namespace GSO_Library.Dtos;

public class ArrangementRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Arrangers { get; set; }
    public List<string>? Composers { get; set; }
    public string? Key { get; set; }
    public int? DurationSeconds { get; set; }
    public int? Year { get; set; }
}
