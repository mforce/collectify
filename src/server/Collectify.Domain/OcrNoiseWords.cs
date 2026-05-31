namespace Collectify.Domain;

/// <summary>
/// Per-category word lists that strip platform/brand/format/rating noise
/// from OCR tokens before building metadata-provider search queries.
/// Lives in Domain because it is pure reference data with zero infra deps.
/// </summary>
public static class OcrNoiseWords
{
    /// <summary>Words that appear on virtually every game cover but carry no title signal.</summary>
    public static readonly HashSet<string> Games = new(StringComparer.OrdinalIgnoreCase)
    {
        // Platforms
        "nintendo", "switch", "switch2", "playstation", "ps1", "ps2", "ps3",
        "ps4", "ps5", "psp", "psvita", "xbox", "xbox360", "xboxone", "xbox series",
        "xbox series x", "xbox series s", "nintendo switch", "pc", "windows",
        "mac", "macos", "linux", "steam", "steam deck", "nintendo 3ds", "3ds",
        "game boy", "gameboy", "game boy advance", "gba", "nds",
        "nintendo ds", "wii", "wii u", "wiiu", "n64", "nintendo 64",
        "gamecube", "game cube", "nes", "snes", "super nintendo",
        "sega", "genesis", "mega drive", "saturn", "dreamcast",

        // ESRB / PEGI ratings
        "esrb", "pegi", "everyone", "everyone 10+", "everyones 10+", "teen",
        "mature", "mature 17+", "adults only", "ao", "rating pending", "rp",
        "enfants", "et", "adultes", "jeunesse",

        // Generic cover labels
        "championship", "edition", "deluxe", "ultimate", "complete",
        "definitive", "standard", "gold", "platinum", "special",
        "anniversary", "remastered", "remake", "enhanced", " collectors",

        // Publisher / imprint labels
        "nickelodeon", "outright", "games", "rbes", "of", "og",

        // Format indicators that leak onto covers
        "blu-ray", "4k", "uhd", "hdr", "dvd",
    };

    /// <summary>Words common on movie discs/boxes that dilute title searches.</summary>
    public static readonly HashSet<string> Movies = new(StringComparer.OrdinalIgnoreCase)
    {
        // Studios / distributors
        "paramount", "universal", "warner bros", "warner brothers", "disney",
        "pixar", "marvel", "dc", "sony", "fox", "20th century", "lumière",

        // Formats
        "dvd", "blu-ray", "blu ray", "4k", "uhd", "uhd blu-ray",
        "hdr", "dolby vision", "dolby atmos", "dts",

        // Edition labels
        "edition", "deluxe", "collector", "collectors", "complete",
        "director", "directors", "directors cut", "unrated", "extended",
        "special", "anniversary", "remastered", "restored", "definitive",
        "limited", "steelbook", "se",

        // Ratings
        "mpaa", "pg", "pg-13", "pg13", "r", "nc-17", "nc17", "g",
        "not rated", "unrated",

        // Generic labels
        "based on", "the book", "novel", "screenplay",
        "a film by", "from the creators", "inspired by",
        "all regions", "region free",
    };

    /// <summary>Words common on album sleeves that dilute title searches.</summary>
    public static readonly HashSet<string> Music = new(StringComparer.OrdinalIgnoreCase)
    {
        // Formats
        "vinyl", "lp", "cd", "cassette", "cd single", "12 inch",
        "7 inch", "picture disc", "gatefold",

        // Edition labels
        "edition", "deluxe", "expanded", "anniversary", "remastered",
        "remix", "remixed", "remixes", "remixed & extended",
        "live", "unplugged", "acoustic", "original",
        "original recording", "original mix",

        // Catalog / printing noise
        "stereo", "mono", "digital remaster", "hybrid sacd", "sacd",
        "import", "promo", "promotion", "white label", "test pressing",

        // Generic labels
        "produced by", "featuring", "feat", "with", "vs",
        "the album", "album",
    };
}
