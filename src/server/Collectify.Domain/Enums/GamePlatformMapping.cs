namespace Collectify.Domain.Enums;

/// <summary>
/// Best-effort resolver from free-form platform strings (IGDB names, the
/// old free-text column, manual entries) into the canonical
/// <see cref="GamePlatform"/> enum. Returns null when nothing matches so
/// callers can decide whether to fall back to <see cref="GamePlatform.Other"/>
/// (migration) or leave the field unset (lookup result -> form dropdown).
///
/// Matching rules:
///   * Case-insensitive, whitespace-trimmed, punctuation-tolerant.
///   * Each enum value carries a small array of accepted aliases; the
///     first match wins. Aliases are normalised the same way the input
///     is, so adding "PlayStation 5" implicitly accepts "playstation5",
///     "PLAYSTATION 5", " ps 5 " etc.
/// </summary>
public static class GamePlatformMapping
{
    private static readonly (GamePlatform Value, string[] Aliases)[] Aliases = new[]
    {
        // A Steam Deck runs SteamOS (Linux) / Windows and plays desktop-PC
        // games, so it classifies as Pc. The "how you got it" dimension is
        // IsDigital + DigitalStore (Steam), not the platform.
        (GamePlatform.Pc, new[] { "pc", "windows", "microsoft windows", "steam deck", "steamdeck" }),
        (GamePlatform.Mac, new[] { "mac", "macos", "mac os", "macintosh", "osx" }),
        (GamePlatform.Linux, new[] { "linux" }),
        (GamePlatform.Mobile, new[] { "mobile", "ios", "iphone", "ipad", "android" }),

        (GamePlatform.XboxOriginal, new[] { "xbox", "xbox original", "original xbox" }),
        (GamePlatform.Xbox360, new[] { "xbox 360", "x360", "360" }),
        (GamePlatform.XboxOne, new[] { "xbox one", "xboxone", "xbone" }),
        (GamePlatform.XboxSeriesXS, new[] { "xbox series x", "xbox series s", "xbox series x s", "xbox series xs", "xbox series", "xsx", "xss" }),

        (GamePlatform.Ps1, new[] { "ps1", "psx", "playstation", "playstation 1", "playstation one", "psone" }),
        (GamePlatform.Ps2, new[] { "ps2", "playstation 2" }),
        (GamePlatform.Ps3, new[] { "ps3", "playstation 3" }),
        (GamePlatform.Ps4, new[] { "ps4", "playstation 4" }),
        (GamePlatform.Ps5, new[] { "ps5", "playstation 5" }),
        (GamePlatform.Psp, new[] { "psp", "playstation portable" }),
        (GamePlatform.PsVita, new[] { "psvita", "vita", "playstation vita" }),

        (GamePlatform.Nes, new[] { "nes", "nintendo entertainment system", "famicom" }),
        (GamePlatform.Snes, new[] { "snes", "super nintendo", "super nintendo entertainment system", "super famicom" }),
        (GamePlatform.N64, new[] { "n64", "nintendo 64" }),
        (GamePlatform.GameCube, new[] { "gamecube", "game cube", "ngc", "gcn" }),
        (GamePlatform.Wii, new[] { "wii" }),
        (GamePlatform.WiiU, new[] { "wii u", "wiiu" }),
        (GamePlatform.Switch, new[] { "switch", "nintendo switch", "nsw" }),
        (GamePlatform.Switch2, new[] { "switch 2", "nintendo switch 2", "switch2" }),

        (GamePlatform.GameBoy, new[] { "game boy", "gameboy", "gb" }),
        (GamePlatform.GameBoyColor, new[] { "game boy color", "gameboy color", "gbc" }),
        (GamePlatform.GameBoyAdvance, new[] { "game boy advance", "gameboy advance", "gba" }),
        (GamePlatform.NintendoDs, new[] { "nintendo ds", "ds", "nds" }),
        (GamePlatform.Nintendo3Ds, new[] { "nintendo 3ds", "3ds", "n3ds" }),

        (GamePlatform.SegaGenesis, new[] { "genesis", "sega genesis", "mega drive", "sega mega drive" }),
        (GamePlatform.SegaSaturn, new[] { "saturn", "sega saturn" }),
        (GamePlatform.SegaDreamcast, new[] { "dreamcast", "sega dreamcast" }),
    };

    private static readonly Dictionary<string, GamePlatform> Lookup = BuildLookup();

    public static GamePlatform? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Lookup.TryGetValue(Normalize(raw), out var value) ? value : null;
    }

    private static Dictionary<string, GamePlatform> BuildLookup()
    {
        // Multiple aliases that normalise to the same string would crash
        // the dictionary builder; first writer wins via the loop instead.
        var d = new Dictionary<string, GamePlatform>(StringComparer.Ordinal);
        foreach (var (value, aliases) in Aliases)
        {
            foreach (var alias in aliases)
            {
                var key = Normalize(alias);
                if (!d.ContainsKey(key)) d[key] = value;
            }
        }
        return d;
    }

    private static string Normalize(string raw)
    {
        // Lowercase + drop every non-alphanumeric. Makes "PlayStation 5",
        // "playstation-5", " ps_5 ", "ps5", "PS 5" all collapse to the
        // same "ps5" key -- spaces and punctuation are noise here, not
        // signal (Xbox 360 vs xbox360 should both match).
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
