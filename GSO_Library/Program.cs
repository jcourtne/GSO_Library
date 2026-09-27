using System.Text;
using System.Text.Json.Serialization;
using GSO_Library.Configuration;
using GSO_Library.Data;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Allow DateTime with TIMESTAMPTZ columns (Npgsql 10 requires this for DateTime instead of DateTimeOffset)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Configure file upload settings
var fileUploadSettings = builder.Configuration.GetSection("FileUpload").Get<FileUploadSettings>() ?? new FileUploadSettings();
builder.Services.AddSingleton(fileUploadSettings);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = fileUploadSettings.MaxFileSizeBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = fileUploadSettings.MaxFileSizeBytes;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
              .AllowCredentials();
    });
});

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new TrimmingStringJsonConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure Dapper to map snake_case columns to PascalCase properties
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
Dapper.SqlMapper.AddTypeHandler(new GSO_Library.Data.DateOnlyTypeHandler());

// Add DbContext (Identity-only, using Npgsql)
builder.Services.AddDbContext<GSOLibraryContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Dapper connection factory
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not configured");
builder.Services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<GSOLibraryContext>()
.AddDefaultTokenProviders();

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
if (builder.Environment.EnvironmentName is not ("Development" or "Testing"))
{
    const string JwtPlaceholderKey = "your-super-secret-key-min-32-characters-long-change-this!";
    if (secretKey == JwtPlaceholderKey || Encoding.UTF8.GetByteCount(secretKey) < 32)
        throw new InvalidOperationException("JWT secret key is insecure: replace the placeholder value with a strong secret before starting the application.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add Token Service
builder.Services.AddScoped<ITokenService, TokenService>();

// Add Audit Service
builder.Services.AddScoped<IAuditService, AuditService>();

// Add File Storage Service (GCS in production when GCS:BucketName is set, local disk otherwise)
// Singleton: both implementations are stateless/thread-safe, and StorageClient is meant to
// be created once and reused rather than rebuilt on every request.
if (!string.IsNullOrEmpty(builder.Configuration["GCS:BucketName"]))
    builder.Services.AddSingleton<IFileStorageService, GcsFileStorageService>();
else
    builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add Repositories
builder.Services.AddScoped<ArrangementRepository>();
builder.Services.AddScoped<ArrangementFileRepository>();
builder.Services.AddScoped<GameRepository>();
builder.Services.AddScoped<SeriesRepository>();
builder.Services.AddScoped<InstrumentRepository>();
builder.Services.AddScoped<InstrumentFamilyRepository>();
builder.Services.AddScoped<PerformanceRepository>();
builder.Services.AddScoped<PerformanceFileRepository>();
builder.Services.AddScoped<EnsembleRepository>();
builder.Services.AddScoped<SeasonRepository>();
builder.Services.AddScoped<SeasonShareZipRepository>();
builder.Services.AddScoped<InstrumentSortOrderRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuditEventRepository>();

builder.Services.AddScoped<ISeasonZipCacheService, SeasonZipCacheService>();
builder.Services.AddScoped<IEnsembleAccessService, EnsembleAccessService>();

// Pre-generate the "download all" share zip when a season share link is configured
builder.Services.AddSingleton<ISeasonZipWarmupQueue, SeasonZipWarmupQueue>();
builder.Services.AddScoped<ISeasonZipWarmupService, SeasonZipWarmupService>();
builder.Services.AddHostedService<SeasonZipWarmupBackgroundService>();

// Periodic cleanup of stale cached season-share zips
builder.Services.Configure<SeasonZipCacheOptions>(builder.Configuration.GetSection("SeasonZipCache"));
builder.Services.AddHostedService<SeasonZipCleanupService>();

var app = builder.Build();

// Resolve Application Default Credentials once, up front, before any hosted service
// (SeasonZipCleanupService, SeasonZipWarmupBackgroundService) or request can race the
// Cloud Run metadata server. Google.Apis.Auth caches a negative GCE-detection result for
// the life of the process, so a lost race here would otherwise break GCS for the
// container's entire lifetime.
if (!string.IsNullOrEmpty(builder.Configuration["GCS:BucketName"]))
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await GoogleCredential.GetApplicationDefaultAsync();
            break;
        }
        catch (InvalidOperationException) when (attempt < 5)
        {
            await Task.Delay(TimeSpan.FromSeconds(attempt));
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

// Seed roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in GSO_Library.Models.Roles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

// Seed users from seed_users.json (file is gitignored)
await UserSeeder.SeedUsersAsync(app);

app.Run();

public partial class Program { }
