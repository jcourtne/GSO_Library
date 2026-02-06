using System.Data;
using Dapper;

namespace GSO_Library.Data;

public static class DbHelpers
{
    /// <summary>
    /// Queries with a WHERE column IN (...) clause, using ANY(@param) for PostgreSQL
    /// and Dapper's IN @param expansion for SQLite.
    /// The sql should use "= ANY(@Param)" syntax. For SQLite it is rewritten to "IN @Param"
    /// (without parentheses) so Dapper can expand the list.
    /// </summary>
    public static async Task<IEnumerable<T>> QueryInListAsync<T>(this IDbConnection connection, string sql, object? param = null)
    {
        var typeName = connection.GetType().Name;
        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
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
