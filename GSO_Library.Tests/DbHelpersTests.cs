using Dapper;
using GSO_Library.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GSO_Library.Tests;

public class DbHelpersTests
{
    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        connection.Execute(@"
            PRAGMA foreign_keys = ON;
            CREATE TABLE parents (id INTEGER PRIMARY KEY);
            CREATE TABLE children (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                parent_id INTEGER REFERENCES parents(id));");
        return connection;
    }

    [Fact]
    public void IsUniqueConstraintViolation_Sqlite_TrueForUniqueViolation()
    {
        using var connection = OpenConnection();
        connection.Execute("INSERT INTO children (name) VALUES ('a')");
        var ex = Assert.ThrowsAny<SqliteException>(() => connection.Execute("INSERT INTO children (name) VALUES ('a')"));
        Assert.True(ex.IsUniqueConstraintViolation());
    }

    [Fact]
    public void IsUniqueConstraintViolation_Sqlite_FalseForOtherConstraintViolations()
    {
        using var connection = OpenConnection();
        var fk = Assert.ThrowsAny<SqliteException>(() => connection.Execute("INSERT INTO children (name, parent_id) VALUES ('b', 999)"));
        Assert.False(fk.IsUniqueConstraintViolation());
        var notNull = Assert.ThrowsAny<SqliteException>(() => connection.Execute("INSERT INTO children (name) VALUES (NULL)"));
        Assert.False(notNull.IsUniqueConstraintViolation());
    }
}
