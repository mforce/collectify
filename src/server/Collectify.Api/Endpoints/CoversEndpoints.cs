using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Collectify.Api.Endpoints;

public static class CoversEndpoints
{
    // Hashes are SHA-256-derived; resources at /covers/{hash} are
    // immutable, so we can cache them aggressively. One year +
    // "immutable" tells the browser it never needs to revalidate.
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";



    public record UploadResponse(string ImagePath);

    /// <summary>
    /// Streams cover-image bytes from the CoverImages table by content hash.
    /// Intentionally not auth-required so img tags work without a fetch
    /// dance; the hash is 16 hex chars derived from a URL the user already
    /// saw in their own collection, so they're effectively unguessable.
    ///
    /// Hash-keyed = immutable. Sets a strong ETag of the hash itself and
    /// a one-year immutable Cache-Control. Conditional GETs that already
    /// know the ETag short-circuit to a bare 304 -- no DB read for the
    /// BLOB column, just headers.
    /// </summary>
    public static IEndpointRouteBuilder MapCoversEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/covers/{hash}", async (string hash, CollectifyDbContext db, HttpContext http, CancellationToken ct) =>
        {
            // Public contract is a flat 16-char hex hash. Reject anything
            // else before touching the DB.
            if (string.IsNullOrEmpty(hash) || hash.Length is < 8 or > 32) return Results.NotFound();
            for (var i = 0; i < hash.Length; i++)
            {
                var c = hash[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return Results.NotFound();
            }

            // ETag is the hash itself, quoted (RFC 9110 §8.8.3). If the
            // client already has it, skip the BLOB read entirely.
            var etag = $"\"{hash}\"";
            if (IfNoneMatchHit(http.Request.Headers.IfNoneMatch, etag))
            {
                http.Response.Headers[HeaderNames.ETag] = etag;
                http.Response.Headers[HeaderNames.CacheControl] = ImmutableCacheControl;
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            var entry = await db.CoverImages.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Hash == hash, ct);
            if (entry is null) return Results.NotFound();

            http.Response.Headers[HeaderNames.ETag] = etag;
            http.Response.Headers[HeaderNames.CacheControl] = ImmutableCacheControl;
            return Results.File(entry.Bytes, entry.ContentType);
        });

        // Multipart upload for "Change cover" in the form. Auth-required
        // so anonymous clients can't burn space; content-addressable
        // storage means two uploads of the same bytes return the same
        // /covers/{hash} URL.
        app.MapPost("/api/covers", async (
            [FromForm(Name = "file")] IFormFile? file,
            ICoverImageStore store,
            CancellationToken ct) =>
        {
            // Shared validation: content-type, size, magic bytes.
            var result = await ImageUploadValidator.ValidateAndReadAsync(file, ct);
            if (result.Error is not null) return result.Error;
            byte[] bytes = result.Bytes!;

            // file and file.ContentType are non-null: validated above.
#pragma warning disable CS8602
            var imagePath = await store.StoreBytesAsync(bytes, file.ContentType, ct);
#pragma warning restore CS8602
            return Results.Ok(new UploadResponse(imagePath));
        })
        .RequireAuthorization()
        // Form binding needs the antiforgery to be either disabled or
        // satisfied by a token. Single-user same-origin install +
        // cookie SameSite=Lax already protects this surface; disable
        // antiforgery for the upload so the SPA's plain FormData POST
        // works without an extra token round-trip.
        .DisableAntiforgery();

        return app;
    }

    private static bool IfNoneMatchHit(StringValues ifNoneMatch, string etag)
    {
        if (StringValues.IsNullOrEmpty(ifNoneMatch)) return false;
        // Headers can be a comma-delimited list. RFC also allows '*'
        // (which matches any current representation -- we honour it for
        // completeness even though /covers/{hash} resources are
        // immutable, so a hit is the right answer either way).
        foreach (var raw in ifNoneMatch)
        {
            if (raw is null) continue;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part == "*") return true;
                // Be tolerant of weak validators ("W/\"hash\"") even
                // though we only emit strong ones.
                var trimmed = part.StartsWith("W/", StringComparison.Ordinal) ? part[2..] : part;
                if (trimmed.Equals(etag, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

}
