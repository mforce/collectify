using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

public class Game
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
    public bool IsDigital { get; set; }
    public DigitalStore? DigitalStore { get; set; }
    public string? Barcode { get; set; }

    public string? IgdbId { get; set; }
    public string? ImagePath { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

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
