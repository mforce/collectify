using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

public class Movie
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public int? Year { get; set; }
    public MovieFormat Formats { get; set; } = MovieFormat.None;
    public string? Director { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? Studio { get; set; }
    public string? Genres { get; set; }
    public string? Barcode { get; set; }

    public string? TmdbId { get; set; }
    public string? ImdbId { get; set; }
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

    public WatchStatus WatchStatus { get; set; } = WatchStatus.Unwatched;
    public DateOnly? LastWatchedOn { get; set; }
    public int WatchCount { get; set; }

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
