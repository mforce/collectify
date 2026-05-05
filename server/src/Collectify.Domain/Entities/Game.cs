using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

public class Game
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public int? Year { get; set; }
    public string? Publisher { get; set; }
    public string? Developer { get; set; }
    public bool IsDigital { get; set; }
    public DigitalStore? DigitalStore { get; set; }
    public string? Barcode { get; set; }

    public string? IgdbId { get; set; }
    public string? ImagePath { get; set; }
    public string? Notes { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
