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
    public DbSet<CoverImage> CoverImages => Set<CoverImage>();
    public DbSet<GameStoreConnection> GameStoreConnections => Set<GameStoreConnection>();
    public DbSet<GameStoreOwnedTitle> GameStoreOwnedTitles => Set<GameStoreOwnedTitle>();
    public DbSet<SteamAuthRequest> SteamAuthRequests => Set<SteamAuthRequest>();

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
            // Alternate key so GameStoreOwnedTitle can hold an
            // ownership-preserving composite FK (GameId, OwnerId) that
            // guarantees a ledger row only references a game owned by the
            // same user. Id remains the PK; this is a redundant-for-lookup
            // unique key that exists to anchor the FK.
            e.HasAlternateKey(g => new { g.Id, g.OwnerId });
            // DLC -> base game self-reference (provider-agnostic). OwnerId
            // scoping is enforced by the relationship: the composite FK
            // (ParentGameId, OwnerId) references the parent's (Id, OwnerId)
            // alternate key, so a DLC child can only point at a base game
            // owned by the SAME user — Alice's DLC can never reference Bob's
            // base game, even if the field is populated later. Restrict so
            // deleting a base game with DLC children is an explicit call, not
            // a silent cascade or nulling of the DLC's parent.
            e.HasOne(g => g.ParentGame)
             .WithMany(g => g.Dlc)
             .HasForeignKey(g => new { g.ParentGameId, g.OwnerId })
             .HasPrincipalKey(g => new { g.Id, g.OwnerId })
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(g => g.ParentGameId);
        });

        builder.Entity<Tag>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(t => new { t.OwnerId, t.Name }).IsUnique();
        });

        builder.Entity<CoverImage>(e =>
        {
            e.HasKey(c => c.Hash);
            e.Property(c => c.Hash).HasMaxLength(32);
            e.Property(c => c.ContentType).HasMaxLength(64).IsRequired();
        });

        builder.Entity<GameStoreConnection>(e =>
        {
            e.Property(c => c.ExternalAccountId).HasMaxLength(64).IsRequired();
            e.Property(c => c.ExternalDisplayName).HasMaxLength(200);
            // One linked account per owner + store. OwnerId is the leading
            // column, so no separate OwnerId index is needed.
            e.HasIndex(c => new { c.OwnerId, c.Store }).IsUnique();
        });

        builder.Entity<GameStoreOwnedTitle>(e =>
        {
            e.Property(t => t.ExternalGameId).HasMaxLength(32).IsRequired();
            e.Property(t => t.ExternalAccountId).HasMaxLength(64);
            e.Property(t => t.ParentExternalGameId).HasMaxLength(32);
            e.Property(t => t.Title).HasMaxLength(500).IsRequired();
            // Natural idempotency key. OwnerId is the leading column, so no
            // separate OwnerId index is needed.
            e.HasIndex(t => new { t.OwnerId, t.Store, t.ExternalGameId }).IsUnique();
            // Ownership-preserving composite FK: a ledger row can only point
            // at a Game owned by the same user. Restrict (no auto SET NULL):
            // the games-DELETE path nulls GameId explicitly first, so a
            // delete never trips a NOT NULL violation on OwnerId.
            e.HasOne<Game>()
             .WithMany()
             .HasForeignKey(t => new { t.GameId, t.OwnerId })
             .HasPrincipalKey(g => new { g.Id, g.OwnerId })
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SteamAuthRequest>(e =>
        {
            e.HasKey(r => r.StateHash);
            e.Property(r => r.StateHash).HasMaxLength(64);
            e.HasIndex(r => r.ExpiresAt);
        });
    }
}
