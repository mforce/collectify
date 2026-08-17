namespace Collectify.Infrastructure.Store;

/// <summary>Strict SteamID64 parsing helpers shared across the OpenID flow.</summary>
public static class SteamId64
{
    /// <summary>
    /// Parses a Steam Community OpenID claimed-id URL's terminal path segment
    /// as a positive 64-bit decimal SteamID. Rejects extra segments, query
    /// strings, fragments, lookalike hosts, empty, and non-numeric tails so a
    /// crafted URL can't smuggle a bogus id.
    /// </summary>
    public static string? FromClaimedId(string? claimedId)
    {
        if (string.IsNullOrWhiteSpace(claimedId)) return null;
        if (!Uri.TryCreate(claimedId, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps) return null;   // never trust a downgraded http claimed-id
        if (uri.Query.Length > 0 || uri.Fragment.Length > 0) return null;

        // Allow the documented canonical Steam Community /id/<steamid64>
        // forms only; everything else (extra path segments, different hosts)
        // is rejected here by the terminal-segment requirement.
        if (!uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase)) return null;

        var segs = uri.Segments.Where(s => s.Length > 1).Select(s => s.TrimEnd('/')).ToArray();
        if (segs.Length != 3) return null;                    // /openid/id/<id>
        if (segs[0] != "openid" || segs[1] != "id") return null;

        var id = segs[2];
        if (id.Length == 0 || !id.All(char.IsAsciiDigit)) return null;
        if (!ulong.TryParse(id, out var value) || value == 0 || value > long.MaxValue) return null;
        return id;
    }
}
