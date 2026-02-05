namespace GSO_Library.Models;

public class AuditEvent
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? TargetUsername { get; set; }
    public string? IpAddress { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
}
