using System.Security.Cryptography;
using System.Text;
using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Collectify.Infrastructure.Lookup.Images;

/// <summary>
/// Materialises a remote cover-image URL into a row in the CoverImages
/// table and returns the public URL the SPA should embed in
/// <c>&lt;img&gt;</c> tags. Storage lives in the SQLite database alongside
/// every other entity so a single backup of <c>collectify.db</c> is a
/// complete snapshot of the collection.
///
/// The contract is forgiving by design: null / blank / already-local paths
/// pass through unchanged so call sites can wrap every Save without
/// branching, and download failures fall back to the original remote URL
/// (the browser can still load it directly) rather than blocking the save.
/// </summary>
public interface ICoverImageStore
{
    Task<string?> EnsureLocalAsync(string? imagePath, CancellationToken ct = default);

    /// <summary>
    /// Persists raw image bytes (e.g. from a multipart upload) and returns
    /// the public <c>/covers/{hash}</c> URL the SPA can embed. The hash
    /// is derived from the bytes themselves, so two uploads of the same
    /// image dedupe to one row regardless of original filename. Callers
    /// are expected to have validated <paramref name="contentType"/> +
    /// size + magic bytes before calling.
    /// </summary>
    Task<string> StoreBytesAsync(byte[] bytes, string contentType, CancellationToken ct = default);
}

public sealed class CoverImageStore : ICoverImageStore
{
    public const string HttpClientName = "covers";

    /// <summary>Hard cap on how many bytes of a remote cover we'll download. A cover
    /// is a kilobyte-to-low-megabyte image; anything bigger is a misconfigured/wrong
    /// response, not a real cover, and would bloat the CoverImages table (which lives
    /// in the same DB as everything else).</summary>
    internal const int MaxDownloadBytes = 15 * 1024 * 1024; // 15 MB

    private readonly CollectifyDbContext _db;
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<CoverImageStore> _log;

    public CoverImageStore(CollectifyDbContext db, IHttpClientFactory factory, ILogger<CoverImageStore> log)
    {
        _db = db;
        _factory = factory;
        _log = log;
    }

    public async Task<string?> EnsureLocalAsync(string? imagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;
        if (!IsRemoteUrl(imagePath)) return imagePath;

        var hash = HashUrl(imagePath);
        var publicUrl = $"/covers/{hash}";

        if (await _db.CoverImages.AsNoTracking().AnyAsync(c => c.Hash == hash, ct))
            return publicUrl;

        try
        {
            var http = _factory.CreateClient(HttpClientName);
            using var response = await http.GetAsync(imagePath, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            // Sized, bounded read: never pull the whole body into memory unbounded.
            var bytes = await ReadBoundedAsync(response, ct);
            if (bytes is null) return imagePath; // too large -> fall back to the remote URL

            var contentType = response.Content.Headers.ContentType?.MediaType
                              ?? GuessContentType(imagePath);
            // Reject non-image responses (an HTML/error page returning 200 must not
            // become a permanent local cover). Magic bytes are the authority.
            var magicType = TryDetectImageMagic(bytes);
            if (magicType is null || !IsAllowedImageType(contentType, magicType))
            {
                _log.LogWarning("Steam cover download for {Url} did not yield a recognized image (content-type: {Ct}); skipping", imagePath, contentType);
                return imagePath;
            }

            _db.CoverImages.Add(new CoverImage
            {
                Hash = hash,
                ContentType = contentType,
                Bytes = bytes,
                AddedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
            return publicUrl;
        }
        catch (DbUpdateException)
        {
            // A concurrent request stored the same hash first. The row is
            // there, our payload would have been identical (same content
            // hash); just discard our copy (detaching the still-Added entity so
            // a later SaveChangesAsync on this context won't retry a duplicate
            // insert) and return the public URL.
            _db.ChangeTracker.Clear();
            return publicUrl;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to cache cover image {Url}; falling back to the remote URL", imagePath);
            return imagePath;
        }
    }

    /// <summary>
    /// Reads the response body into a byte array, but never more than
    /// <see cref="MaxDownloadBytes"/>. Returns null when the body exceeds the
    /// cap (caller should fall back to the remote URL rather than store a huge /
    /// wrong payload). Also rejects a Content-Length that already exceeds the cap.
    /// </summary>
    private static async Task<byte[]?> ReadBoundedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is { } len && len > MaxDownloadBytes)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(chunk, ct);
            if (read == 0) break;
            total += read;
            if (total > MaxDownloadBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>Caps how many redirects a cover download may follow (defense against
    /// a hostile/looping URL chain).</summary>
    public const int MaxRedirects = 3;

    public async Task<string> StoreBytesAsync(byte[] bytes, string contentType, CancellationToken ct = default)
    {
        var hash = HashBytes(bytes);
        var publicUrl = $"/covers/{hash}";

        // Content-addressable: a row with the same hash means the same
        // bytes are already there. Skip the second insert; the hash is
        // what /covers/{hash} keys off, so the public URL is identical.
        if (await _db.CoverImages.AsNoTracking().AnyAsync(c => c.Hash == hash, ct))
            return publicUrl;

        try
        {
            _db.CoverImages.Add(new CoverImage
            {
                Hash = hash,
                ContentType = contentType,
                Bytes = bytes,
                AddedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two concurrent uploads of the same bytes race; the loser's
            // payload is identical, so we can swallow and return the same
            // URL (the winner's row is already in the table). Detach the
            // still-Added entity so a later SaveChangesAsync on this context
            // won't retry a duplicate insert.
            _db.ChangeTracker.Clear();
        }
        return publicUrl;
    }

    private static bool IsRemoteUrl(string s) =>
        s.StartsWith("http://", StringComparison.Ordinal) || s.StartsWith("https://", StringComparison.Ordinal);

    private static string HashUrl(string url)
    {
        var sha = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(sha)[..16].ToLowerInvariant();
    }

    private static string HashBytes(byte[] bytes)
    {
        var sha = SHA256.HashData(bytes);
        return Convert.ToHexString(sha)[..16].ToLowerInvariant();
    }

    private static string GuessContentType(string url)
    {
        var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg",
        };
    }

    private static bool IsAllowedImageType(string? headerType, string magicType)
    {
        // Header type (if present) must be an image we accept; the magic-byte
        // type is always the final authority, so a text/html header can't smuggle
        // a stored page, and a mismatched header (e.g. image/png served as octet)
        // is tolerated as long as the bytes are a real image.
        if (!string.IsNullOrWhiteSpace(headerType) && !headerType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return false;
        return magicType is "image/jpeg" or "image/png" or "image/webp" or "image/gif";
    }

    /// <summary>
    /// Detects a real image from its leading magic bytes; returns null for
    /// anything that isn't a recognised image (HTML, JSON, empty, etc.).
    /// </summary>
    private static string? TryDetectImageMagic(byte[] bytes)
    {
        // JPEG: FF D8 FF
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "image/png";
        // GIF: "GIF87a" / "GIF89a"
        if (bytes.Length >= 3 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F') return "image/gif";
        // WebP: "RIFF" .... "WEBP"
        if (bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return "image/webp";

        return null;
    }
}
