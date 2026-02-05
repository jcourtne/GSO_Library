using System.Data;
using Dapper;

namespace GSO_Library.Data;

public static class DbHelpers
{
    /// <summary>
    /// Executes an INSERT statement and returns the generated id.
    /// Uses RETURNING id for PostgreSQL, last_insert_rowid() for SQLite.
    /// </summary>
    public static async Task<int> InsertReturningIdAsync(this IDbConnection connection, string sql, object? param = null)
    {
        var typeName = connection.GetType().Name;
        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // SQLite: execute the insert, then query last_insert_rowid()
            await connection.ExecuteAsync(sql, param);
            return await connection.ExecuteScalarAsync<int>("SELECT last_insert_rowid()");
        }
        else
        {
            // PostgreSQL: append RETURNING id and use ExecuteScalar
            return await connection.ExecuteScalarAsync<int>(sql + " RETURNING id", param);
        }
    }
}
