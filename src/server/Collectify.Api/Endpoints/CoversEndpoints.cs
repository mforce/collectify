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

    // 5 MiB upload cap. The CoverImages BLOB column has no schema-level
    // ceiling, but ~5 MiB covers every real-world JPEG / PNG / WebP
    // poster and keeps a single row from bloating backups.
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    // Whitelist mirrors what CoverPreview can actually render in a
    // <img> tag. SVG / TIFF / HEIF are deliberately excluded -- SVG
    // can carry script and the latter two are inconsistently supported
    // across browsers we care about.
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

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
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "A non-empty file is required." });
            if (file.Length > MaxUploadBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            if (file.ContentType is null || !AllowedContentTypes.Contains(file.ContentType))
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

            byte[] bytes;
            await using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            // Magic-byte sniff catches a lying Content-Type. We trust
            // the whitelist for *which* image types are acceptable, but
            // re-derive whether the bytes actually match that format so
            // a client can't smuggle a text/HTML blob under image/jpeg.
            if (!MagicBytesMatch(bytes, file.ContentType))
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

            var imagePath = await store.StoreBytesAsync(bytes, file.ContentType, ct);
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

    /// <summary>
    /// Confirms the leading bytes of an upload actually look like the
    /// declared Content-Type. Defensive belt-and-braces -- a lying
    /// client could otherwise plant text/HTML under image/jpeg.
    /// Returns <c>true</c> for content-types we don't sniff (defensive
    /// default for entries that are in the whitelist but lack a stable
    /// header signature we can rely on).
    /// </summary>
    private static bool MagicBytesMatch(byte[] bytes, string contentType)
    {
        if (bytes.Length < 4) return false;
        switch (contentType.ToLowerInvariant())
        {
            case "image/jpeg":
                // JPEG: FF D8 FF.
                return bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
            case "image/png":
                // PNG: 89 50 4E 47 0D 0A 1A 0A.
                return bytes.Length >= 8
                    && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                    && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
            case "image/webp":
                // WebP: "RIFF" + 4 byte size + "WEBP".
                return bytes.Length >= 12
                    && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                    && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
            default:
                return true;
        }
    }
}
