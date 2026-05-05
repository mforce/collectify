using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

public class MusicAlbum
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public int? Year { get; set; }
    public MusicFormat Format { get; set; } = MusicFormat.Cd;
    public string? Label { get; set; }
    public string? Genres { get; set; }
    public string? Barcode { get; set; }

    public string? MusicBrainzReleaseId { get; set; }
    public string? DiscogsId { get; set; }
    public string? ImagePath { get; set; }
    public string? Notes { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
