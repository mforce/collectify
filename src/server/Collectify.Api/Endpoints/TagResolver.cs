using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

internal static class TagResolver
{
    public static async Task<List<Tag>> ResolveAsync(
        CollectifyDbContext db,
        string ownerId,
        IEnumerable<string>? names)
    {
        if (names is null) return [];

        var normalized = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        if (normalized.Count == 0) return [];

        var existing = await db.Tags
            .Where(t => t.OwnerId == ownerId && normalized.Contains(t.Name))
            .ToListAsync();

        var missing = normalized.Except(existing.Select(t => t.Name)).ToList();
        foreach (var name in missing)
        {
            var t = new Tag { OwnerId = ownerId, Name = name };
            db.Tags.Add(t);
            existing.Add(t);
        }
        return existing;
    }

    public static string[] ToNameArray(IEnumerable<Tag> tags) =>
        tags.Select(t => t.Name).OrderBy(n => n).ToArray();
}
