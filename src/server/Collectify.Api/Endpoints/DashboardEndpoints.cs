using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class DashboardEndpoints
{
    private const int RecentLimit = 6;

    public record DashboardCounts(int Movies, int Music, int Games);

    public record DashboardRecent(
        string Type, // "movies" | "music" | "games"
        int Id,
        string Title,
        int? Year,
        string? ImagePath,
        DateTime AddedAt);

    public record DashboardSummary(DashboardCounts Counts, IReadOnlyList<DashboardRecent> Recent);

    /// <summary>
    /// One-shot dashboard payload: per-type counts + the N most recent
    /// additions across all three types, owner-scoped. Replaces the
    /// pull-all-three-lists dance the dashboard page used to do, so the
    /// home page render is a single round-trip and doesn't drag entire
    /// collections over the wire.
    /// </summary>
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (
            CollectifyDbContext db,
            UserManager<AppUser> users,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;

            // Three lightweight COUNT(*) per-owner queries. Run them in
            // parallel against the same DbContext is unsafe (EF doesn't
            // allow concurrent operations on one context), so await
            // each in sequence.
            var movieCount = await db.Movies.CountAsync(m => m.OwnerId == ownerId, ct);
            var musicCount = await db.MusicAlbums.CountAsync(a => a.OwnerId == ownerId, ct);
            var gameCount = await db.Games.CountAsync(g => g.OwnerId == ownerId, ct);

            // Each query asks for `RecentLimit` rows -- when we
            // interleave them by AddedAt we keep at most `RecentLimit`
            // of the combined stream, so larger pulls would be wasted.
            var recentMovies = await db.Movies.AsNoTracking()
                .Where(m => m.OwnerId == ownerId)
                .OrderByDescending(m => m.AddedAt)
                .Take(RecentLimit)
                .Select(m => new DashboardRecent("movies", m.Id, m.Title, m.Year, m.ImagePath, m.AddedAt))
                .ToListAsync(ct);
            var recentMusic = await db.MusicAlbums.AsNoTracking()
                .Where(a => a.OwnerId == ownerId)
                .OrderByDescending(a => a.AddedAt)
                .Take(RecentLimit)
                .Select(a => new DashboardRecent("music", a.Id, a.Title, a.Year, a.ImagePath, a.AddedAt))
                .ToListAsync(ct);
            var recentGames = await db.Games.AsNoTracking()
                .Where(g => g.OwnerId == ownerId)
                .OrderByDescending(g => g.AddedAt)
                .Take(RecentLimit)
                .Select(g => new DashboardRecent("games", g.Id, g.Title, g.Year, g.ImagePath, g.AddedAt))
                .ToListAsync(ct);

            var recent = recentMovies.Concat(recentMusic).Concat(recentGames)
                .OrderByDescending(r => r.AddedAt)
                .Take(RecentLimit)
                .ToList();

            return Results.Ok(new DashboardSummary(
                new DashboardCounts(movieCount, musicCount, gameCount),
                recent));
        }).RequireAuthorization();

        return app;
    }
}
