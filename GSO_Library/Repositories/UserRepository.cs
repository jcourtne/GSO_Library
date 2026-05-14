using Dapper;
using GSO_Library.Data;

namespace GSO_Library.Repositories;

public class UserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DateTime?> GetLastLoginAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT MAX(created_at) FROM audit_events WHERE event_type = 'LoginSuccess' AND username = @Username",
            new { Username = username });
    }

    public async Task<Dictionary<string, DateTime?>> GetLastLoginsAsync(IEnumerable<string> usernames)
    {
        var list = usernames.ToList();
        if (list.Count == 0) return [];

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryInListAsync<LastLoginRow>(
            "SELECT username, MAX(created_at) AS last_login FROM audit_events WHERE event_type = 'LoginSuccess' AND username = ANY(@Ids) GROUP BY username",
            new { Ids = list.ToArray() });

        return rows.ToDictionary(r => r.Username, r => r.LastLogin);
    }

    private class LastLoginRow
    {
        public string Username { get; set; } = "";
        public DateTime? LastLogin { get; set; }
    }
}
