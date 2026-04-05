namespace GSO_Library.Models;

public class InstrumentSortOrder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public List<Instrument> Instruments { get; set; } = [];
}
