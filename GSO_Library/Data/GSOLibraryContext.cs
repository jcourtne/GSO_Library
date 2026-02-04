using GSO_Library.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GSO_Library.Data;

public class GSOLibraryContext : IdentityDbContext<ApplicationUser>
{
    public GSOLibraryContext(DbContextOptions<GSOLibraryContext> options) : base(options)
    {
    }

    public DbSet<Arrangement> Arrangements { get; set; }
    public DbSet<ArrangementFile> ArrangementFiles { get; set; }
    public DbSet<Game> Games { get; set; }
    public DbSet<Series> Series { get; set; }
    public DbSet<Performance> Performances { get; set; }
    public DbSet<Instrument> Instruments { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Arrangement-Game many-to-many relationship
        modelBuilder.Entity<Arrangement>()
            .HasMany(a => a.Games)
            .WithMany(g => g.Arrangements)
            .UsingEntity(j => j.ToTable("ArrangementGames"));

        // Configure Arrangement-Instrument many-to-many relationship
        modelBuilder.Entity<Arrangement>()
            .HasMany(a => a.Instruments)
            .WithMany(i => i.Arrangements)
            .UsingEntity(j => j.ToTable("ArrangementInstruments"));

        // Configure Game-Series one-to-many relationship
        modelBuilder.Entity<Game>()
            .HasOne(g => g.Series)
            .WithMany(s => s.Games)
            .HasForeignKey(g => g.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Arrangement-Performance one-to-many relationship
        modelBuilder.Entity<Performance>()
            .HasOne(p => p.Arrangement)
            .WithMany(a => a.Performances)
            .HasForeignKey(p => p.ArrangementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Arrangement-ArrangementFile one-to-many relationship
        modelBuilder.Entity<ArrangementFile>()
            .HasOne(f => f.Arrangement)
            .WithMany(a => a.Files)
            .HasForeignKey(f => f.ArrangementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure RefreshToken
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();
    }
}
