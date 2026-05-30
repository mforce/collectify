namespace Collectify.Infrastructure.Lookup.Vision;

/// <summary>
/// Extracts provider IDs from trusted-domain URLs returned by
/// WEB_DETECTION. Parses uri.Host and uri.AbsolutePath segments
/// instead of regex-matching the full URL — tolerates trailing slashes,
/// query strings, and URLs without slugs.
/// </summary>
public static class UrlRouter
{
    public static string? ExtractTmdbId(Uri uri)
    {
        if (!IsHost(uri, "themoviedb.org") && !IsHost(uri, "www.themoviedb.org"))
            return null;

        // /movie/27205-inception or /movie/27205
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "movie")
            return null;

        // First path segment after /movie/ — strip trailing slug if present
        var idPart = segments[1].Split('-')[0];
        return long.TryParse(idPart, out _) ? idPart : null;
    }

    public static string? ExtractMusicBrainzReleaseId(Uri uri)
    {
        if (!IsHost(uri, "musicbrainz.org"))
            return null;

        // /release/<mbid>
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "release")
            return null;

        var mbid = segments[1];
        // Validate MBID shape: 8-4-4-4-12 hex
        if (mbid.Length != 36)
            return null;
        for (var i = 0; i < mbid.Length; i++)
        {
            var c = mbid[i];
            if (i == 8 || i == 13 || i == 18 || i == 23)
            {
                if (c != '-') return null;
            }
            else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            {
                return null;
            }
        }
        return mbid;
    }

    /// <summary>
    /// Resolves an IGDB URL. Returns a search slug from the path so the
    /// caller can run a title search (IGDB has no slug-to-id endpoint).
    /// </summary>
    public static UrlResolution? ResolveIgdbUrl(Uri uri)
    {
        if (!IsHost(uri, "igdb.com") && !IsHost(uri, "www.igdb.com"))
            return null;

        // /games/the-witcher-3-wild-hunt or /games/1942
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "games")
            return null;

        var slugOrId = segments[1];

        // If it's a plain numeric ID, use it directly.
        if (long.TryParse(slugOrId, out var id) && id > 0)
            return new UrlResolution(id.ToString(), null);

        // Otherwise treat it as a slug (hyphen-separated) — the caller
        // will search by this string in its fuzzy search endpoint.
        return new UrlResolution(null, slugOrId);
    }

    /// <summary>Either a direct provider ID or a slug for fuzzy search.</summary>
    public sealed class UrlResolution
    {
        public string? Id { get; }
        public string? SearchSlug { get; }
        public UrlResolution(string? id, string? searchSlug) { Id = id; SearchSlug = searchSlug; }
    }

    /// <summary>Resolves a Wikipedia URL to a page-title slug for search.</summary>
    public static UrlResolution? ResolveWikipediaGameUrl(Uri uri)
    {
        if (!IsHost(uri, "en.wikipedia.org") && !IsHost(uri, "wikipedia.org"))
            return null;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // /wiki/Page_Title_Name or /w/index.php?title=Page_Title_Name
        if (segments.Length >= 2 && segments[0] == "wiki")
        {
            var slug = segments[1];
            // Skip special pages like /wiki/Special:Search, /wiki/Talk:...
            if (slug.Contains(':', StringComparison.Ordinal))
                return null;
            if (slug.Length == 0)
                return null;

            // Underscores are spaces; keep hyphens as-is for titles like "The-Witcher-3-Wild-Hunt"
            var title = slug.Replace('_', ' ').Trim();
            return new UrlResolution(null, title);
        }

        return null;
    }

    /// <summary>Resolves a PlayStation Store URL to a search slug.</summary>
    public static UrlResolution? ResolvePlayStationStoreUrl(Uri uri)
    {
        if (!IsHost(uri, "store.playstation.com") && !IsHost(uri, "www.store.playstation.com"))
            return null;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // /product/PS5-LittleBigPlanet or /product/PS5/CUSA28598_00-LITTLEBIGPLANET0000/
        if (segments.Length >= 2 && segments[0] == "product")
        {
            var slug = segments[1];

            // If the slug contains underscores, it's a SKU slug like CUSA28598_00-LITTLEBIGPLANET0000.
            // Try to extract a readable title by removing leading SKU-like prefixes.
            var title = SlugToTitle(slug);
            if (!string.IsNullOrWhiteSpace(title))
                return new UrlResolution(null, title);
        }

        return null;
    }

    /// <summary>Resolves a Target.com URL to a search slug.</summary>
    public static UrlResolution? ResolveTargetUrl(Uri uri)
    {
        if (!IsHost(uri, "www.target.com") && !IsHost(uri, "target.com"))
            return null;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // /p/paw-patrol-rescue-wheels-championship-nintendo-switch/-/A-94802526
        if (segments.Length >= 2 && segments[0] == "p")
        {
            var slug = segments[1];
            var title = SlugToTitle(slug);
            if (!string.IsNullOrWhiteSpace(title))
                return new UrlResolution(null, title);
        }

        return null;
    }

    /// <summary>Resolves a Walmart.com URL to a search slug.</summary>
    public static UrlResolution? ResolveWalmartUrl(Uri uri)
    {
        if (!IsHost(uri, "www.walmart.com") && !IsHost(uri, "walmart.com"))
            return null;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // /ip/PAW-Patrol-Rescue-Wheels-Championship-Nintendo-Switch/16904655730
        if (segments.Length >= 2 && segments[0] == "ip")
        {
            var slug = segments[1];
            var title = SlugToTitle(slug);
            if (!string.IsNullOrWhiteSpace(title))
                return new UrlResolution(null, title);
        }

        return null;
    }

    /// <summary>Resolves an Amazon.com URL to a search slug.</summary>
    public static UrlResolution? ResolveAmazonUrl(Uri uri)
    {
        var host = uri.Host;
        if (!host.EndsWith(".amazon.com", StringComparison.Ordinal) && !host.Equals("amazon.com", StringComparison.Ordinal))
            return null;

        var segments = uri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // /dp/ASIN or /gp/product/ASIN or /Video-Game-Title/dp/ASIN
        if (segments.Length >= 2)
        {
            // Try to find a slug-like segment (contains hyphens, not just ASIN)
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var seg = segments[i];
                if (seg.Contains('-', StringComparison.Ordinal) && seg.Length > 3)
                {
                    var title = SlugToTitle(seg);
                    if (!string.IsNullOrWhiteSpace(title))
                        return new UrlResolution(null, title);
                }
            }
        }

        return null;
    }

    /// <summary>Converts a URL slug (hyphen-separated) to a readable title.</summary>
    private static string? SlugToTitle(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        // Split on hyphens (word separators in URLs). Each part is title-cased.
        var parts = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var title = string.Join(" ", parts.Select(p =>
        {
            // Replace underscores with spaces within a part
            p = p.Replace('_', ' ').Trim();
            if (p.Length == 0) return string.Empty;
            // Title-case: capitalize first letter, lowercase rest.
            // Preserves intentional casing like "PAW" -> "Paw" which is fine
            // for fuzzy search.
            return char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant();
        }));

        return title;
    }

    private static bool IsHost(Uri uri, string expected)
        => uri.Host.Equals(expected, StringComparison.Ordinal);
}
