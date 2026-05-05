using Collectify.Domain.Entities;
using Collectify.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Infrastructure.Data;

public class CollectifyDbContext : IdentityDbContext<AppUser>
{
    public CollectifyDbContext(DbContextOptions<CollectifyDbContext> options) : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<MusicAlbum> MusicAlbums => Set<MusicAlbum>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<LookupCacheEntry> LookupCache => Set<LookupCacheEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Movie>(e =>
        {
            e.HasIndex(m => m.OwnerId);
            e.HasIndex(m => m.Title);
            e.HasIndex(m => m.Barcode);
            e.Property(m => m.Title).HasMaxLength(500).IsRequired();
        });

        builder.Entity<MusicAlbum>(e =>
        {
            e.HasIndex(m => m.OwnerId);
            e.HasIndex(m => m.Title);
            e.HasIndex(m => m.ArtistName);
            e.HasIndex(m => m.Barcode);
            e.Property(m => m.Title).HasMaxLength(500).IsRequired();
            e.Property(m => m.ArtistName).HasMaxLength(500).IsRequired();
        });

        builder.Entity<Game>(e =>
        {
            e.HasIndex(g => g.OwnerId);
            e.HasIndex(g => g.Title);
            e.HasIndex(g => g.Barcode);
            e.Property(g => g.Title).HasMaxLength(500).IsRequired();
        });

        builder.Entity<LookupCacheEntry>(e =>
        {
            e.HasIndex(l => new { l.Provider, l.Key }).IsUnique();
        });
    }
}
