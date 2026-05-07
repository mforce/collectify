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
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<LookupCacheEntry> LookupCache => Set<LookupCacheEntry>();
    public DbSet<CoverImage> CoverImages => Set<CoverImage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Movie>(e =>
        {
            e.HasIndex(m => m.OwnerId);
            e.HasIndex(m => m.Title);
            e.HasIndex(m => m.Barcode);
            e.Property(m => m.Title).HasMaxLength(500).IsRequired();
            e.Property(m => m.AcquisitionCurrency).HasMaxLength(3);
            e.Property(m => m.AcquisitionPrice).HasColumnType("decimal(18,2)");
        });

        builder.Entity<MusicAlbum>(e =>
        {
            e.HasIndex(m => m.OwnerId);
            e.HasIndex(m => m.Title);
            e.HasIndex(m => m.ArtistName);
            e.HasIndex(m => m.Barcode);
            e.Property(m => m.Title).HasMaxLength(500).IsRequired();
            e.Property(m => m.ArtistName).HasMaxLength(500).IsRequired();
            e.Property(m => m.AcquisitionCurrency).HasMaxLength(3);
            e.Property(m => m.AcquisitionPrice).HasColumnType("decimal(18,2)");
        });

        builder.Entity<Game>(e =>
        {
            e.HasIndex(g => g.OwnerId);
            e.HasIndex(g => g.Title);
            e.HasIndex(g => g.Barcode);
            e.Property(g => g.Title).HasMaxLength(500).IsRequired();
            e.Property(g => g.AcquisitionCurrency).HasMaxLength(3);
            e.Property(g => g.AcquisitionPrice).HasColumnType("decimal(18,2)");
        });

        builder.Entity<Tag>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(t => new { t.OwnerId, t.Name }).IsUnique();
        });

        builder.Entity<LookupCacheEntry>(e =>
        {
            e.HasIndex(l => new { l.Provider, l.Key }).IsUnique();
        });

        builder.Entity<CoverImage>(e =>
        {
            e.HasKey(c => c.Hash);
            e.Property(c => c.Hash).HasMaxLength(32);
            e.Property(c => c.ContentType).HasMaxLength(64).IsRequired();
        });
    }
}
