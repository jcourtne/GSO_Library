using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if (args.Length < 4)
{
    Console.WriteLine("Usage: dotnet run -- <connectionString> <username> <email> <password>");
    return 1;
}

var connectionString = args[0];
var username = args[1];
var email = args[2];
var password = args[3];

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var services = new ServiceCollection();

services.AddLogging();
services.AddDbContext<GSOLibraryContext>(options => options.UseNpgsql(connectionString));
services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
})
.AddEntityFrameworkStores<GSOLibraryContext>()
.AddDefaultTokenProviders();

var sp = services.BuildServiceProvider();

using var scope = sp.CreateScope();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

// Ensure roles exist
foreach (var role in new[] { "Admin", "Editor", "User" })
{
    if (!await roleManager.RoleExistsAsync(role))
        await roleManager.CreateAsync(new IdentityRole(role));
}

// Check if user already exists
if (await userManager.FindByNameAsync(username) != null)
{
    Console.WriteLine($"User '{username}' already exists.");
    return 0;
}

var user = new ApplicationUser
{
    UserName = username,
    Email = email,
    FirstName = "Admin",
    LastName = "User",
};

var result = await userManager.CreateAsync(user, password);
if (!result.Succeeded)
{
    Console.WriteLine("Failed to create user:");
    foreach (var error in result.Errors)
        Console.WriteLine($"  {error.Description}");
    return 1;
}

await userManager.AddToRoleAsync(user, "Admin");
Console.WriteLine($"Admin user '{username}' created successfully.");
return 0;
