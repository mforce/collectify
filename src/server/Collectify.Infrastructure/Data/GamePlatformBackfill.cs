using Collectify.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Infrastructure.Data;

/// <summary>
/// One-shot backfill that turns the free-text values preserved in
/// <c>Game.PlatformLegacy</c> at the <c>ConvertGamePlatformToEnum</c>
/// migration step into proper <see cref="GamePlatform"/> values.
/// Runs at app startup; idempotent because each pass only touches rows
/// whose <c>PlatformLegacy</c> still has a value, and clears it on a hit.
/// Values that don't map are left as-is so the user can re-classify by
/// hand.
/// </summary>
public static class GamePlatformBackfill
{
    public static async Task<int> RunAsync(CollectifyDbContext db, CancellationToken ct = default)
    {
        // Pull only rows that still have a legacy string. After the
        // migration this is "every row that had a non-empty Platform
        // before"; on subsequent boots the set is empty so the cost is a
        // single index-friendly query that returns nothing.
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

        if (resolved > 0) await db.SaveChangesAsync(ct);
        return resolved;
    }
}
