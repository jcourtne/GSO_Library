using System.Data;
using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using GSO_Library.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GSO_Library.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"file:testdb_{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAliveConnection;

    public CustomWebApplicationFactory()
    {
        // Open a keep-alive connection so the shared in-memory database persists.
        _keepAliveConnection = new SqliteConnection($"DataSource={_dbName}");
        _keepAliveConnection.Open();

        // Enable foreign keys
        using var cmd = _keepAliveConnection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();

        // Create Identity schema via EF
        var options = new DbContextOptionsBuilder<GSOLibraryContext>()
            .UseSqlite(_keepAliveConnection)
            .Options;
        using var ctx = new GSOLibraryContext(options);
        ctx.Database.EnsureCreated();

        // Create application tables (SQLite-compatible DDL)
        CreateApplicationTables(_keepAliveConnection);
    }

    private static void CreateApplicationTables(SqliteConnection connection)
    {
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS series (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                description TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                created_by TEXT
            );

            CREATE TABLE IF NOT EXISTS games (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                description TEXT,
                series_id INTEGER NOT NULL REFERENCES series(id) ON DELETE CASCADE,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                created_by TEXT
            );

            CREATE TABLE IF NOT EXISTS arrangements (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                description TEXT,
                arranger TEXT,
                composer TEXT,
                key TEXT,
                duration_seconds INTEGER,
                year INTEGER,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                created_by TEXT
            );

            CREATE TABLE IF NOT EXISTS arrangement_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_name TEXT NOT NULL,
                stored_file_name TEXT NOT NULL,
                content_type TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                uploaded_at TEXT NOT NULL,
                arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
                created_by TEXT
            );

            CREATE TABLE IF NOT EXISTS instruments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                created_by TEXT
            );

            CREATE TABLE IF NOT EXISTS performances (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                link TEXT NOT NULL,
                performance_date TEXT,
                notes TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                created_by TEXT
            );

            CREATE TABLE IF NOT EXISTS arrangement_games (
                arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
                game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
                PRIMARY KEY (arrangement_id, game_id)
            );

            CREATE TABLE IF NOT EXISTS arrangement_instruments (
                arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
                instrument_id INTEGER NOT NULL REFERENCES instruments(id) ON DELETE CASCADE,
                PRIMARY KEY (arrangement_id, instrument_id)
            );

            CREATE TABLE IF NOT EXISTS arrangement_performances (
                arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
                performance_id INTEGER NOT NULL REFERENCES performances(id) ON DELETE CASCADE,
                PRIMARY KEY (arrangement_id, performance_id)
            );

            CREATE TABLE IF NOT EXISTS audit_events (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                event_type      TEXT NOT NULL,
                username        TEXT,
                target_username TEXT,
                ip_address      TEXT,
                detail          TEXT,
                created_at      TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove all EF Core registrations for GSOLibraryContext
            services.RemoveAll(typeof(DbContextOptions<GSOLibraryContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(GSOLibraryContext));

            // Remove IDbContextOptionsConfiguration registrations (carries the UseNpgsql config)
            var optionConfigDescriptors = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>))
                .ToList();
            foreach (var d in optionConfigDescriptors)
                services.Remove(d);

            // Remove existing IFileStorageService and IDbConnectionFactory
            services.RemoveAll(typeof(IFileStorageService));
            services.RemoveAll(typeof(IDbConnectionFactory));

            // Re-register GSOLibraryContext with the shared SQLite connection
            services.AddDbContext<GSOLibraryContext>(options =>
                options.UseSqlite($"DataSource={_dbName}"));

            // Register SQLite connection factory for Dapper
            services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(_dbName));

            // Register in-memory file storage stub
            services.AddScoped<IFileStorageService, InMemoryFileStorageService>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Program.cs already seeded the roles during host startup.
        // Now seed test users.
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        SeedUsersAsync(userManager, roleManager).GetAwaiter().GetResult();

        return host;
    }

    private static async Task SeedUsersAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Ensure roles (in case Program.cs seeding somehow didn't run)
        foreach (var role in new[] { "Admin", "Editor", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await CreateUserIfMissing(userManager, "testadmin", "admin@test.com", "Admin123!", "Test", "Admin", "Admin");
        await CreateUserIfMissing(userManager, "testeditor", "editor@test.com", "Editor123!", "Test", "Editor", "Editor");
        await CreateUserIfMissing(userManager, "testuser", "user@test.com", "User1234!", "Test", "User", "User");
    }

    private static async Task CreateUserIfMissing(
        UserManager<ApplicationUser> userManager,
        string userName, string email, string password,
        string firstName, string lastName, string role)
    {
        if (await userManager.FindByNameAsync(userName) != null)
            return;

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAliveConnection.Dispose();
    }
}

/// <summary>
/// SQLite connection factory for integration tests.
/// Creates connections to a shared in-memory SQLite database.
/// </summary>
public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _dbName;

    public SqliteConnectionFactory(string dbName)
    {
        _dbName = dbName;
    }

    public IDbConnection CreateConnection()
    {
        var connection = new SqliteConnection($"DataSource={_dbName}");
        connection.Open();
        // Enable foreign keys for each new connection
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
        return connection;
    }
}

/// <summary>
/// In-memory file storage for tests, avoiding disk I/O.
/// </summary>
public class InMemoryFileStorageService : IFileStorageService
{
    private static readonly Dictionary<string, byte[]> _files = new();

    private static string GetKey(int arrangementId, string storedFileName)
        => $"{arrangementId}/{storedFileName}";

    public async Task<string> SaveFileAsync(int arrangementId, string storedFileName, Stream content)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        _files[GetKey(arrangementId, storedFileName)] = ms.ToArray();
        return GetKey(arrangementId, storedFileName);
    }

    public Task<Stream> GetFileAsync(int arrangementId, string storedFileName)
    {
        var key = GetKey(arrangementId, storedFileName);
        if (!_files.TryGetValue(key, out var bytes))
            throw new FileNotFoundException("File not found.", key);

        Stream stream = new MemoryStream(bytes);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(int arrangementId, string storedFileName)
    {
        _files.Remove(GetKey(arrangementId, storedFileName));
        return Task.CompletedTask;
    }
}
