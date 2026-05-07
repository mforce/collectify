namespace Collectify.Domain.Entities;

/// <summary>
/// Cached cover-image bytes keyed by the content-hash slice used in the
/// public /covers/{hash} URL. Two items pointing at the same poster share
/// a single row; the JsonResponse / Bytes blob lives in the SQLite file
/// alongside the rest of the data, so a backup of collectify.db is a full
/// backup of the collection.
/// </summary>
public class CoverImage
{
    public string Hash { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Bytes { get; set; } = [];
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
