namespace GSO_Library.Models;

public class Instrument
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<Arrangement> Arrangements { get; set; } = [];
}
