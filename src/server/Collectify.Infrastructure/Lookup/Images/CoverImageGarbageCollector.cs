using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Collectify.Infrastructure.Lookup.Images;

/// <summary>
/// One-shot sweep that removes <c>CoverImages</c> rows no longer
/// referenced by any movie / album / game <c>ImagePath</c>. Run at app
/// startup after migrations so a long-running install with lots of
/// metadata churn (lookup-then-replace, scan-then-rescan, etc.) doesn't
/// grow the CoverImages table without bound.
///
/// Safety nets:
/// - A grace window (default 1h) keeps very recently-added covers even
///   if they don't have a referrer yet -- protects against a race
///   where the cover lands in the table before the owning entity is
///   written.
/// - Reference extraction is conservative: anything that looks like
///   <c>/covers/&lt;hex&gt;</c> in an ImagePath counts as a reference.
///   Other column values (legacy filesystem paths, raw provider URLs
///   that didn't get re-hosted) don't pull any covers.
/// </summary>
public sealed class CoverImageGarbageCollector
{
    private readonly TimeProvider _clock;
    private readonly ILogger<CoverImageGarbageCollector> _log;

    public CoverImageGarbageCollector(TimeProvider clock, ILogger<CoverImageGarbageCollector> log)
    {
        _clock = clock;
        _log = log;
    }

    /// <summary>
    /// Sweeps orphaned covers. Returns the number of rows deleted.
    /// </summary>
    /// <param name="grace">Minimum age a cover must have before it
    /// becomes eligible for deletion. Defaults to 1 hour so newly-added
    /// covers can't be GC'd mid-save.</param>
    public async Task<int> SweepAsync(Data.CollectifyDbContext db, TimeSpan? grace = null, CancellationToken ct = default)
    {
        var graceWindow = grace ?? TimeSpan.FromHours(1);
        var cutoff = _clock.GetUtcNow().UtcDateTime - graceWindow;

        // Pull every cover path stored on any owning entity. We could
        // do this in one big UNION query but the three tables are
        // narrow and per-user, so the round-trip is cheap and the C#
        // is easier to read.
        var moviePaths = await db.Movies.AsNoTracking()
            .Where(m => m.ImagePath != null).Select(m => m.ImagePath!).ToListAsync(ct);
        var musicPaths = await db.MusicAlbums.AsNoTracking()
            .Where(a => a.ImagePath != null).Select(a => a.ImagePath!).ToListAsync(ct);
        var gamePaths = await db.Games.AsNoTracking()
            .Where(g => g.ImagePath != null).Select(g => g.ImagePath!).ToListAsync(ct);

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in moviePaths.Concat(musicPaths).Concat(gamePaths))
        {
            if (TryExtractHash(p, out var hash)) referenced.Add(hash);
        }

        // ExecuteDeleteAsync emits a single DELETE with the predicate,
        // no tracking, no per-row roundtrips. The .NET enumeration of
        // `referenced.Contains(...)` is translated to a SQL `WHERE Hash
        // NOT IN (…)` which is fine at our cardinality.
        var deleted = await db.CoverImages
            .Where(c => c.AddedAt < cutoff && !referenced.Contains(c.Hash))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            _log.LogInformation("Cover GC: removed {Count} orphaned cover(s)", deleted);
        return deleted;
    }

    /// <summary>
    /// Pulls the 8-32 char hex hash out of an <c>ImagePath</c> like
    /// <c>/covers/abcd1234ef</c>. Returns false for any other shape
    /// (raw provider URLs, empty strings, etc.) so foreign references
    /// don't accidentally pin a cover row.
    /// </summary>
    public static bool TryExtractHash(string? imagePath, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrEmpty(imagePath)) return false;
        const string prefix = "/covers/";
        if (!imagePath.StartsWith(prefix, StringComparison.Ordinal)) return false;

        var candidate = imagePath.AsSpan(prefix.Length);
        if (candidate.Length is < 8 or > 32) return false;
        foreach (var c in candidate)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        hash = candidate.ToString();
        return true;
    }
}
