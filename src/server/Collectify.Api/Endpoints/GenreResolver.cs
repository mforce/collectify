using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Collectify.Tests")]

namespace Collectify.Api.Endpoints;

internal static class GenreResolver
{
    public static async Task<List<Genre>> ResolveAsync(
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

        var existing = await db.Genres
            .Where(g => g.OwnerId == ownerId && normalized.Contains(g.Name))
            .ToListAsync();

        var missing = normalized.Except(existing.Select(g => g.Name)).ToList();
        foreach (var name in missing)
        {
            var g = new Genre { OwnerId = ownerId, Name = name };
            db.Genres.Add(g);
            existing.Add(g);
        }
        return existing;
    }

    public static string[] ToNameArray(IEnumerable<Genre> genres) =>
        genres.Select(g => g.Name).OrderBy(n => n).ToArray();
}
