using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Services;

public class AuditService : IAuditService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IDbConnectionFactory connectionFactory, ILogger<AuditService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task LogAsync(string eventType, string? username, string? targetUsername,
                               string? ipAddress, string? detail)
    {
        var logLevel = eventType switch
        {
            AuditEventType.LoginFailure => LogLevel.Warning,
            AuditEventType.AccountDisable => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, "Audit: {EventType} user={Username} target={TargetUsername} ip={IpAddress} detail={Detail}",
            eventType, username, targetUsername, ipAddress, detail);

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync(
                """
                INSERT INTO audit_events (event_type, username, target_username, ip_address, detail)
                VALUES (@EventType, @Username, @TargetUsername, @IpAddress, @Detail)
                """,
                new { EventType = eventType, Username = username, TargetUsername = targetUsername, IpAddress = ipAddress, Detail = detail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit event {EventType}", eventType);
        }
    }
}
