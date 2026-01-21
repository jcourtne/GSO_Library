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
    public DbSet<Game> Games { get; set; }
    public DbSet<Series> Series { get; set; }
    public DbSet<Performance> Performances { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Arrangement-Game many-to-many relationship
        modelBuilder.Entity<Arrangement>()
            .HasMany(a => a.Games)
            .WithMany(g => g.Arrangements)
            .UsingEntity(j => j.ToTable("ArrangementGames"));

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
    }
}
