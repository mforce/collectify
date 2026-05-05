namespace Collectify.Domain.Entities;

public class LookupCacheEntry
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string JsonResponse { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
