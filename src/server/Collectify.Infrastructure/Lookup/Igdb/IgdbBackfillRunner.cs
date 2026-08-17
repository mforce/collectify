using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// Scoped worker that performs a single IGDB backfill sweep: finds every
/// <see cref="Game"/> with <c>IgdbId == null</c>, resolves a confident IGDB
/// match per title (see <see cref="IgdbBackfillPlanner"/>), and fills the
/// metadata plus a locally-cached cover.
///
/// Idempotent: filled games are skipped, so a re-run is a no-op for anything
/// already resolved. Unmatched games are left for manual resolution in the UI —
/// there is deliberately no "attempted" marker (re-sweeps are cheap because
/// lookups hit the ILookupCache).
///
/// Merge semantics (fill-only, never clobber — hardened after code review):
/// every existing value on a Game wins. Backfill only fills fields that are
/// still null, so a Steam import's richer data or a user's hand-entered
/// description / uploaded cover is never overwritten. Only the IGDB linkage
/// (<c>IgdbId</c>) is always written.
///
/// Fail-soft and quota-aware: an unconfigured provider makes the whole sweep a
/// no-op; a per-game failure is isolated (the change tracker is cleared so a
/// failed game's partial state can never be flushed by a later save); the sweep
/// is paced, capped at <see cref="IgdbBackfillOptions.MaxGamesPerSweep"/>, and
/// aborts early when IGDB looks throttled (consecutive empty results).
/// </summary>
public sealed class IgdbBackfillRunner
{
    private readonly CollectifyDbContext _db;
    private readonly IGameMetadataProvider _provider;
    private readonly ICoverImageStore _covers;
    private readonly TimeProvider _clock;
    private readonly IgdbBackfillOptions _options;
    private readonly ILogger<IgdbBackfillRunner> _log;

    public IgdbBackfillRunner(
        CollectifyDbContext db,
        IGameMetadataProvider provider,
        ICoverImageStore covers,
        TimeProvider clock,
        IOptions<IgdbBackfillOptions> options,
        ILogger<IgdbBackfillRunner> log)
    {
        _db = db;
        _provider = provider;
        _covers = covers;
        _clock = clock;
        _options = options.Value;
        _log = log;
    }

    /// <summary>
    /// Run one sweep. Returns how many games were filled and how many were
    /// actually attempted (so the caller can advance the rotation window by the
    /// real amount processed, not a configured cap that may have been cut short
    /// by the throttle abort).
    /// </summary>
    /// <param name="ct">Cancellation token (honoured throughout, incl. pacing).</param>
    /// <param name="offset">
    /// Rotates the start of the per-sweep window so high-id games are never
    /// permanently starved by low-id unmatchable titles at the head of the
    /// queue. There is deliberately NO attempted-marker (issue #132), so the
    /// pending set is ordered by Id and unchanged games would otherwise pin
    /// the window to the lowest ids forever; advancing the offset each sweep
    /// (and wrapping via `offset % count`) guarantees every pending game is
    /// eventually attempted.
    /// </param>
    public async Task<BackfillSweepResult> RunSweepAsync(CancellationToken ct = default, int offset = 0)
    {
        if (!_provider.IsConfigured)
        {
            _log.LogInformation("IGDB backfill skipped: IGDB/Twitch not configured");
            return new BackfillSweepResult(Filled: 0, Attempted: 0);
        }

        // Deterministic, bounded, and NOT tracked: the pending list is only
        // used to drive iteration. Each game is re-read on its own scope/save
        // below, so we never hold the whole set in memory or leave stale
        // tracked state lying around. Personal collections are small, so
        // fetching all pending ids to rotate the window is cheap.
        var pending = await _db.Games.AsNoTracking()
            .Where(g => g.IgdbId == null)
            .OrderBy(g => g.Id)
            .Select(g => g.Id)
            .ToListAsync(ct);
        if (pending.Count == 0) return new BackfillSweepResult(Filled: 0, Attempted: 0);

        // Rotate the window start by `offset` so each sweep reaches a different
        // slice of the (unmatched) pending set instead of always the lowest ids.
        var rotateBy = offset % pending.Count;
        var window = pending.Skip(rotateBy)
            .Concat(pending.Take(rotateBy))
            .Take(_options.MaxGamesPerSweep)
            .ToList();

        var filled = 0;
        var attempted = 0;
        var consecutiveEmpty = 0;

        foreach (var gameId in window)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                attempted++;
                var outcome = await BackfillOneAsync(gameId, ct);

                if (outcome.ProviderReturnedEmpty)
                {
                    // IGDB returned nothing — indistinguishable from throttling
                    // (429 is surfaced as [] by IgdbGameProvider). Bump the
                    // counter; if it climbs to the threshold we abort below.
                    consecutiveEmpty++;
                }
                else if (outcome.WasFilled)
                {
                    filled++;
                    consecutiveEmpty = 0;
                }
                else
                {
                    // Provider returned a non-empty result but nothing was a
                    // confident match — leave for manual resolution. A nonempty
                    // response proves IGDB is NOT throttled, so reset the
                    // empty-result streak (otherwise empties separated by real
                    // responses would accumulate and falsely abort the sweep).
                    consecutiveEmpty = 0;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // shutdown — stop promptly, don't drain the rest
            }
            catch (Exception ex)
            {
                // Fail-soft: isolate this game's failure. Clear the change
                // tracker so any mutation made before the throw can never be
                // flushed by a later game's save (the shared scoped DbContext
                // hazard). The game's IgdbId is only persisted in the single
                // atomic save, so a failure here leaves it null in the DB and
                // it is simply re-swept next run.
                _db.ChangeTracker.Clear();
                consecutiveEmpty = 0; // a hard failure isn't a throttle signal
                _log.LogWarning(ex, "IGDB backfill failed for game {GameId}; skipping", gameId);
            }

            // Pacing between provider calls (honours cancellation so shutdown
            // can't be held up by a long delay). Tests set PacingDelay to zero
            // to disable this (a fake TimeProvider never advances on its own).
            if (_options.PacingDelay > TimeSpan.Zero)
                await Task.Delay(_options.PacingDelay, _clock, ct);

            if (consecutiveEmpty >= _options.EmptyResultAbortThreshold)
            {
                _log.LogWarning(
                    "IGDB backfill aborting sweep: {N} consecutive empty results (likely IGDB throttling); resuming next interval",
                    consecutiveEmpty);
                break;
            }
        }

        _log.LogInformation("IGDB backfill sweep complete: filled {Filled} of {Pending} pending", filled, pending.Count);
        return new BackfillSweepResult(Filled: filled, Attempted: attempted);
    }

    /// <summary>Result of a single sweep: how many games were filled and attempted.</summary>
    public sealed record BackfillSweepResult(int Filled, int Attempted);

    /// <summary>
    /// Outcome of attempting to backfill a single game.
    /// </summary>
    private sealed record BackfillOutcome(bool WasFilled, bool ProviderReturnedEmpty);

    /// <summary>
    /// Backfill a single game by id.
    /// </summary>
    private async Task<BackfillOutcome> BackfillOneAsync(int gameId, CancellationToken ct)
    {
        // Read just the seed fields we need for matching, AsNoTracking (we don't
        // mutate this instance). The authoritative, fill-only merge happens below
        // against a FRESH tracked load taken after the outbound work.
        var seed = await _db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => new { g.Title, g.Year, g.Platform, g.ImagePath })
            .FirstOrDefaultAsync(ct);
        if (seed is null) return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: false);

        var candidates = await _provider.SearchAsync(seed.Title, ct);
        if (candidates.Count == 0)
        {
            // IGDB returned nothing — likely throttling, signal the caller.
            return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: true);
        }

        var match = IgdbBackfillPlanner.BestMatch(
            new Game { Title = seed.Title, Year = seed.Year, Platform = seed.Platform },
            candidates);
        if (match is null)
        {
            _log.LogDebug("IGDB backfill: no confident match for \"{Title}\", leaving for manual resolution", seed.Title);
            return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: false);
        }

        // Localize the cover NOW, before any further read/apply of the row.
        // CoverImageStore calls SaveChangesAsync internally on the same scoped
        // DbContext, and doing it here — while the game row is untouched — means
        // that internal save never flushes a partially-applied game. The skip
        // decision uses the seed's ImagePath (captured before the outbound work);
        // fill-only would discard the download if an image already exists
        // (avoiding an orphan CoverImages row + needless network I/O).
        string? coverPath = null;
        if (string.IsNullOrWhiteSpace(seed.ImagePath) && !string.IsNullOrWhiteSpace(match.Result.ImageUrl))
            coverPath = await _covers.EnsureLocalAsync(match.Result.ImageUrl, ct);

        // Reload the row TRACKED and apply immediately — with NO awaiting between
        // this load and the single save below. This is the concurrent-edit guard:
        // the fill-only merge runs against the CURRENT committed values (a user
        // may meanwhile have filled Description/Year/Developer or assigned an
        // IgdbId during the search/cover I/O above), and the `IgdbId == null`
        // filter makes an in-flight assignment turn this load into null -> skip,
        // never overwriting it. All the slow I/O has already happened, so the
        // load->apply->save window is microseconds with no await.
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == gameId && g.IgdbId == null, ct);
        if (game is null)
        {
            _log.LogInformation("IGDB backfill skipped for game {GameId}: IGDB id assigned concurrently", gameId);
            return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: false);
        }

        // Revalidate the match inputs against the reloaded row: a user may have
        // renamed the game or edited its year/platform during the search/cover
        // I/O, in which case this match was computed against stale Title/Year/
        // Platform and would link the wrong release. Skip so the (cheap, cached)
        // re-sweep recomputes the match next interval rather than baking in a
        // stale IgdbId.
        if (!string.Equals(game.Title, seed.Title, StringComparison.Ordinal)
            || game.Year != seed.Year
            || game.Platform != seed.Platform)
        {
            _log.LogInformation("IGDB backfill skipped for game {GameId}: title/year/platform changed while matching", gameId);
            return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: false);
        }

        Apply(game, match.Result, coverPath);

        // Single atomic save commits the whole game at once. IgdbId is only
        // persisted here, so a failure before this line leaves the game fully
        // null and re-sweepable.
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("IGDB backfill: linked \"{Title}\" to IGDB id {IgdbId}", game.Title, match.Result.ProviderKey);
        return new BackfillOutcome(WasFilled: true, ProviderReturnedEmpty: false);
    }

    /// <summary>
    /// Fill-only merge: existing values always win; only IgdbId is always
    /// written. Never overwrites a developer/publisher/year/description/cover
    /// the user or a Steam import already set. Never writes Platform — the
    /// existing value (or Other, left for the user) is preserved.
    /// </summary>
    private void Apply(Game game, GameLookupResult r, string? coverPath)
    {
        game.IgdbId = r.ProviderKey;
        game.Developer ??= r.Developer;
        game.Publisher ??= r.Publisher;
        game.Year ??= r.Year;
        if (string.IsNullOrWhiteSpace(game.Description))
            game.Description = ComposeDescription(r.Description, r.Genres);
        if (string.IsNullOrWhiteSpace(game.ImagePath))
            game.ImagePath = coverPath;
        game.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Genres have no dedicated field on Game (issue #132), so they are folded
    /// into the description rather than creating user-visible tags — a
    /// deliberate decision to avoid polluting the global per-owner tag pool.
    /// </summary>
    internal static string? ComposeDescription(string? summary, string? genres)
    {
        var hasSummary = !string.IsNullOrWhiteSpace(summary);
        var hasGenres = !string.IsNullOrWhiteSpace(genres);

        if (hasSummary && hasGenres) return $"{summary}\n\nGenres: {genres}";
        if (hasGenres) return $"Genres: {genres}";
        return hasSummary ? summary : null;
    }
}
