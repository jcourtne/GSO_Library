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
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        // Open the connection once and keep it alive for the factory's lifetime.
        // All DbContext instances will share this same in-memory database.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Create the schema immediately so that Program.cs role seeding
        // (which runs during host startup) finds the tables it needs.
        var options = new DbContextOptionsBuilder<GSOLibraryContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new GSOLibraryContext(options);
        ctx.Database.EnsureCreated();
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

            // Remove IDbContextOptionsConfiguration registrations (carries the UseSqlServer config)
            var optionConfigDescriptors = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>))
                .ToList();
            foreach (var d in optionConfigDescriptors)
                services.Remove(d);

            // Remove existing IFileStorageService
            services.RemoveAll(typeof(IFileStorageService));

            // Re-register GSOLibraryContext with the shared SQLite connection
            services.AddDbContext<GSOLibraryContext>(options =>
                options.UseSqlite(_connection));

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
            _connection.Dispose();
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
