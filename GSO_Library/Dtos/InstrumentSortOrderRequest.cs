using System.ComponentModel.DataAnnotations;

namespace GSO_Library.Dtos;

public class InstrumentSortOrderRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public List<int> InstrumentIds { get; set; } = [];
}
