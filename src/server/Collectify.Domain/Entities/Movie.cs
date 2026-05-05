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
    public string? Notes { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
