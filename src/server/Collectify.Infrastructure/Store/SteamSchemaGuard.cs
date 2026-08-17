using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Infrastructure.Store;

/// <summary>
/// Caches whether the Steam store-import tables actually exist in the current
/// database, determined once at startup.
///
/// Why this exists: SQLite is kept in sync by <c>MigrateAsync()</c>, but the
/// Postgres path uses <c>EnsureCreatedAsync()</c>, which is a no-op against an
/// existing database — so an upgraded existing Postgres install never gains the
/// new <c>GameStoreConnections</c> / <c>GameStoreOwnedTitles</c> /
/// <c>SteamAuthRequests</c> tables. Instead of letting every Steam endpoint crash
/// with an unhandled "undefined table" 500, the endpoints consult this flag and
/// fail soft to the same "Steam import not configured" UX as a missing API key.
/// </summary>
public sealed class SteamSchemaGuard
{
    private bool _ready;

    /// <summary>Whether the Steam store tables are present in the current DB.</summary>
    public bool IsSchemaReady => _ready;

    /// <summary>Set once at startup after the DB is initialised.</summary>
    public void MarkReady(bool ready) => _ready = ready;

    /// <summary>
    /// Provider-agnostic existence check for the Steam tables. Works for both
    /// SQLite and PostgreSQL; intentionally safe to call only after the context
    /// is connected.
    /// </summary>
    public static async Task<bool> DetectAsync(CollectifyDbContext db, CancellationToken ct = default)
    {
        var tables = new[] { "GameStoreConnections", "GameStoreOwnedTitles", "SteamAuthRequests" };
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) is true)
        {
            var master = await db.Database.SqlQueryRaw<string?>(
                "SELECT name FROM sqlite_master WHERE type='table'").ToListAsync(ct);
            return tables.All(t => master.Contains(t));
        }
        // PostgreSQL: to_regclass returns non-null only when the table exists.
        var present = await db.Database.SqlQueryRaw<string?>(
            "SELECT to_regclass('public.\"' || t || '\"') FROM (VALUES ('GameStoreConnections'),('GameStoreOwnedTitles'),('SteamAuthRequests')) AS x(t)")
            .ToListAsync(ct);
        return present.All(r => !string.IsNullOrWhiteSpace(r));
    }
}
