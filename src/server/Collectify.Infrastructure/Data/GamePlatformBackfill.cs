using Collectify.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Infrastructure.Data;

/// <summary>
/// One-shot startup fixup for <c>Game.Platform</c>. Two jobs, both
/// idempotent because each pass only touches rows that still need
/// fixing, and clears the marker on a hit:
///
/// 1. Retire removed enum values. Rows still carrying the integer of an
///    enum member that no longer exists are reset to a valid value.
///    Currently <see cref="RetiredPlatformValues"/> = { 60 }: the
///    <c>SteamDeck</c> member was removed (#103) and its rows reclassify
///    as <c>Pc</c>. This lives here rather than only in the
///    <c>ConvertSteamDeckToPc</c> migration because Postgres builds its
///    schema with <c>EnsureCreated()</c> and never replays migrations,
///    so a migration-only fix would never run there.
/// 2. Resolve the free-text values preserved in <c>Game.PlatformLegacy</c>
///    at the <c>ConvertGamePlatformToEnum</c> migration into proper
///    <see cref="GamePlatform"/> values, clearing the column on a hit.
///    Values that don't map are left as-is so the user can re-classify
///    by hand.
/// </summary>
public static class GamePlatformBackfill
{
    /// <summary>
    /// Persisted <c>Platform</c> integers whose enum member has been
    /// removed, mapped to the value their rows reclassify to. 60 was
    /// <c>GamePlatform.SteamDeck</c> (a PC); see #103.
    /// </summary>
    // Public so EnumParityTests can derive ReservedValues from this single
    // source of truth -- a second hand-maintained copy would drift: retiring
    // a value here and forgetting the test copy would let a later member be
    // assigned the same value and silently clobbered on every boot.
    public static readonly IReadOnlyDictionary<int, GamePlatform> RetiredPlatformValues =
        new Dictionary<int, GamePlatform>
        {
            [60] = GamePlatform.Pc,
        };

    public static async Task<int> RunAsync(CollectifyDbContext db, CancellationToken ct = default)
    {
        // Job 1: retire rows still holding a removed enum value. EF
        // translates this to parameterised SQL, so it is safe on both
        // SQLite and Postgres (unlike raw SQL in a migration, which
        // never runs under Postgres' EnsureCreated path).
        var retired = 0;
        foreach (var (from, to) in RetiredPlatformValues)
        {
            var stale = await db.Games
                .Where(g => g.Platform == (GamePlatform)from)
                .ToListAsync(ct);
            if (stale.Count == 0) continue;
            foreach (var g in stale) g.Platform = to;
            retired += stale.Count;
        }

        // Job 2: resolve any remaining free-text PlatformLegacy values.
        var pending = await db.Games
            .Where(g => g.PlatformLegacy != null)
            .ToListAsync(ct);

        var resolved = 0;
        foreach (var g in pending)
        {
            var mapped = GamePlatformMapping.TryParse(g.PlatformLegacy);
            if (mapped is null) continue;

            g.Platform = mapped.Value;
            g.PlatformLegacy = null;
            resolved++;
        }

        if (retired > 0 || resolved > 0) await db.SaveChangesAsync(ct);
        return retired + resolved;
    }
}
