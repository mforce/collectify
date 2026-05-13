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
            using var response = await http.GetAsync(imagePath, ct);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? GuessContentType(imagePath);

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
            // hash); just discard our copy and return the public URL.
            return publicUrl;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to cache cover image {Url}; falling back to the remote URL", imagePath);
            return imagePath;
        }
    }

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
            // URL (the winner's row is already in the table).
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
}
