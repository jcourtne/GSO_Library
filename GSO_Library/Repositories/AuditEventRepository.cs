using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class AuditEventRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<IEnumerable<string>> GetDistinctUsernamesAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<string>("""
            SELECT DISTINCT username
            FROM audit_events
            WHERE username IS NOT NULL
            ORDER BY username ASC
            """);
    }

    public async Task<IEnumerable<AuditEvent>> GetAllAsync(string[]? eventTypes, string[]? usernames, DateTime? from, DateTime? to)
    {
        using var conn = connectionFactory.CreateConnection();
        var filterByEventTypes = eventTypes is { Length: > 0 };
        var filterByUsernames = usernames is { Length: > 0 };

        if (filterByEventTypes && filterByUsernames)
        {
            return await conn.QueryInListAsync<AuditEvent>("""
                SELECT id, event_type, username, target_username, ip_address, detail, created_at
                FROM audit_events
                WHERE event_type = ANY(@EventTypes)
                  AND username = ANY(@Usernames)
                  AND (@From IS NULL OR created_at >= @From)
                  AND (@To IS NULL OR created_at <= @To)
                ORDER BY created_at DESC
                """, new { EventTypes = eventTypes, Usernames = usernames, From = from, To = to });
        }

        if (filterByEventTypes)
        {
            return await conn.QueryInListAsync<AuditEvent>("""
                SELECT id, event_type, username, target_username, ip_address, detail, created_at
                FROM audit_events
                WHERE event_type = ANY(@EventTypes)
                  AND (@From IS NULL OR created_at >= @From)
                  AND (@To IS NULL OR created_at <= @To)
                ORDER BY created_at DESC
                """, new { EventTypes = eventTypes, From = from, To = to });
        }

        if (filterByUsernames)
        {
            return await conn.QueryInListAsync<AuditEvent>("""
                SELECT id, event_type, username, target_username, ip_address, detail, created_at
                FROM audit_events
                WHERE username = ANY(@Usernames)
                  AND (@From IS NULL OR created_at >= @From)
                  AND (@To IS NULL OR created_at <= @To)
                ORDER BY created_at DESC
                """, new { Usernames = usernames, From = from, To = to });
        }

        return await conn.QueryAsync<AuditEvent>("""
            SELECT id, event_type, username, target_username, ip_address, detail, created_at
            FROM audit_events
            WHERE (@From IS NULL OR created_at >= @From)
              AND (@To IS NULL OR created_at <= @To)
            ORDER BY created_at DESC
            """, new { From = from, To = to });
    }
}
