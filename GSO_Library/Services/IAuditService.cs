namespace GSO_Library.Services;

public interface IAuditService
{
    Task LogAsync(string eventType, string? username, string? targetUsername,
                  string? ipAddress, string? detail);
}
