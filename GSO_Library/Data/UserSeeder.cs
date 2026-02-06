using System.Text.Json;
using GSO_Library.Models;
using Microsoft.AspNetCore.Identity;

namespace GSO_Library.Data;

public static class UserSeeder
{
    public static async Task SeedUsersAsync(WebApplication app)
    {
        var seedFile = app.Configuration["SeedUsersFile"];
        if (string.IsNullOrEmpty(seedFile))
            return;

        var seedPath = Path.IsPathRooted(seedFile)
            ? seedFile
            : Path.Combine(app.Environment.ContentRootPath, seedFile);
        if (!File.Exists(seedPath))
            return;

        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("UserSeeder");

        var json = await File.ReadAllTextAsync(seedPath);
        var seedUsers = JsonSerializer.Deserialize<List<SeedUserEntry>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (seedUsers is null)
            return;

        foreach (var seedUser in seedUsers)
        {
            if (await userManager.FindByNameAsync(seedUser.Username) is not null)
            {
                logger.LogInformation("Seed user '{Username}' already exists, skipping", seedUser.Username);
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = seedUser.Username,
                Email = seedUser.Email,
                FirstName = seedUser.FirstName,
                LastName = seedUser.LastName,
            };

            var result = await userManager.CreateAsync(user, seedUser.Password);
            if (result.Succeeded)
            {
                foreach (var role in seedUser.Roles)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
                logger.LogInformation("Seeded user '{Username}' with roles: {Roles}",
                    seedUser.Username, string.Join(", ", seedUser.Roles));
            }
            else
            {
                logger.LogError("Failed to seed user '{Username}': {Errors}",
                    seedUser.Username, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private record SeedUserEntry(
        string Username,
        string Email,
        string Password,
        string? FirstName,
        string? LastName,
        string[] Roles);
}
