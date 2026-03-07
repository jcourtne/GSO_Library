using System.ComponentModel.DataAnnotations;

namespace GSO_Library.Dtos;

public class ArrangementRequest
{
    [Required]
    [StringLength(500)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Arrangers { get; set; }
    public List<string>? Composers { get; set; }
    public int? DurationSeconds { get; set; }
    public int? Year { get; set; }
}
