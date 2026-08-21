using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

public class Game : ICollectionEntry
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public GamePlatform Platform { get; set; } = GamePlatform.Other;
    /// <summary>
    /// Original free-text platform string preserved at migration time when
    /// it didn't map cleanly to a <see cref="GamePlatform"/>. Lets users
    /// see what they originally typed and re-classify by hand. New rows
    /// don't write to this; it's pre-existing-data-only and is slated for
    /// removal in a follow-up once the legacy values have been resolved.
    /// </summary>
    public string? PlatformLegacy { get; set; }
    public int? Year { get; set; }
    public string? Publisher { get; set; }
    public string? Developer { get; set; }
    public DigitalStore DigitalStores { get; set; } = DigitalStore.None;
    public string? Barcode { get; set; }

    public string? IgdbId { get; set; }
    public string? ImagePath { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    /// <summary>Display age rating string, e.g. "PEGI 16" or "ESRB M".</summary>
    public string? AgeRating { get; set; }

    /// <summary>
    /// If this game is downloadable content (DLC / add-on), the base game it
    /// belongs to. Provider-agnostic: Steam/Xbox/PSN all model DLC as a
    /// separate product linked back to a base game, so a single self-ref is
    /// enough for every storefront. Null for standalone/base games. Populated
    /// once DLC-parent resolution lands (per-provider); today's imports leave
    /// it null (they stay flat, user-managed).
    /// </summary>
    public int? ParentGameId { get; set; }
    public Game? ParentGame { get; set; }
    public ICollection<Game> Dlc { get; set; } = new List<Game>();

    public int? PersonalRating { get; set; }
    public CollectionStatus Status { get; set; } = CollectionStatus.Owned;
    public Condition? Condition { get; set; }

    public DateOnly? AcquiredOn { get; set; }
    public decimal? AcquisitionPrice { get; set; }
    public string? AcquisitionCurrency { get; set; }
    public string? AcquisitionSource { get; set; }

    public CompletionStatus CompletionStatus { get; set; } = CompletionStatus.NotStarted;
    public int? HoursPlayed { get; set; }
    public DateOnly? LastPlayedOn { get; set; }

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
