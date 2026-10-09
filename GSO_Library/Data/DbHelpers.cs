using System.Data;
using Dapper;

namespace GSO_Library.Data;

public static class DbHelpers
{
    /// <summary>
    /// True when the connection is SQLite (integration tests) rather than PostgreSQL.
    /// </summary>
    public static bool IsSqlite(this IDbConnection connection)
        => connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Queries with a WHERE column IN (...) clause, using ANY(@param) for PostgreSQL
    /// and Dapper's IN @param expansion for SQLite.
    /// The sql should use "= ANY(@Param)" syntax. For SQLite it is rewritten to "IN @Param"
    /// (without parentheses) so Dapper can expand the list.
    /// </summary>
    public static async Task<IEnumerable<T>> QueryInListAsync<T>(this IDbConnection connection, string sql, object? param = null)
    {
        if (connection.IsSqlite())
        {
            // Rewrite "= ANY(@Param)" to "IN @Param" for Dapper list expansion
            sql = System.Text.RegularExpressions.Regex.Replace(sql, @"= ANY\((@\w+)\)", "IN $1");
            return await connection.QueryAsync<T>(sql, param);
        }
        else
        {
            return await connection.QueryAsync<T>(sql, param);
        }
    }

    /// <summary>
    /// Executes an INSERT statement and returns the generated id.
    /// Uses RETURNING id for PostgreSQL, last_insert_rowid() for SQLite.
    /// </summary>
    public static async Task<int> InsertReturningIdAsync(this IDbConnection connection, string sql, object? param = null)
    {
        if (connection.IsSqlite())
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

    /// <summary>
    /// True if the exception represents a unique constraint violation, for both PostgreSQL
    /// (Npgsql.PostgresException, SqlState 23505) and SQLite (SqliteException, SqliteExtendedErrorCode
    /// 2067 / SQLITE_CONSTRAINT_UNIQUE — not the base code 19, which also covers FK/NOT NULL/CHECK).
    /// Checked by type name via reflection so this project need not reference the SQLite driver
    /// (test-only) just to recognize its exception type.
    /// </summary>
    public static bool IsUniqueConstraintViolation(this Exception ex)
    {
        var type = ex.GetType();
        return type.Name switch
        {
            "PostgresException" => type.GetProperty("SqlState")?.GetValue(ex) as string == "23505",
            "SqliteException" => type.GetProperty("SqliteExtendedErrorCode")?.GetValue(ex) is 2067,
            _ => false
        };
    }

    /// <summary>
    /// True if a row in <paramref name="table"/> already has this name (case-insensitive), optionally
    /// ignoring the row with id <paramref name="excludeId"/>. <paramref name="table"/> is interpolated
    /// into the SQL, so it must be a constant from code — never user input.
    /// </summary>
    public static async Task<bool> NameExistsAsync(this IDbConnection connection, string table, string name, int? excludeId = null)
    {
        var sql = $"SELECT COUNT(*) FROM {table} WHERE LOWER(name) = LOWER(@Name)";
        if (excludeId.HasValue) sql += " AND id != @ExcludeId";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Name = name, ExcludeId = excludeId });
        return count > 0;
    }
}
