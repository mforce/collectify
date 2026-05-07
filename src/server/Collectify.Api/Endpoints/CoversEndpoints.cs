using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class CoversEndpoints
{
    /// <summary>
    /// Streams cover-image bytes from the CoverImages table by content hash.
    /// Intentionally not auth-required so img tags work without a fetch
    /// dance; the hash is 16 hex chars derived from a URL the user already
    /// saw in their own collection, so they're effectively unguessable.
    /// </summary>
    public static IEndpointRouteBuilder MapCoversEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/covers/{hash}", async (string hash, CollectifyDbContext db, CancellationToken ct) =>
        {
            // Public contract is a flat 16-char hex hash. Reject anything
            // else before touching the DB.
            if (string.IsNullOrEmpty(hash) || hash.Length is < 8 or > 32) return Results.NotFound();
            for (var i = 0; i < hash.Length; i++)
            {
                var c = hash[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return Results.NotFound();
            }

            var entry = await db.CoverImages.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Hash == hash, ct);
            if (entry is null) return Results.NotFound();

            return Results.File(entry.Bytes, entry.ContentType);
        });

        return app;
    }
}
