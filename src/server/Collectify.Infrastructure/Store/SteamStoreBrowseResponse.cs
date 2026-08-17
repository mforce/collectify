using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Store;

/// <summary>
/// DTOs for the modern, keyless, bulk storefront endpoint
/// <c>IStoreBrowseService/GetItems</c>. Returns rich per-app metadata
/// (developer, publisher, release date, description, review/rating summary)
/// for up to ~50 appids per request — the primary bulk metadata source for
/// import. Field names follow the protobuf store schema, not the legacy
/// <c>appdetails</c> store JSON.
/// </summary>
public sealed class SteamStoreBrowseRequestEnvelope
{
    [JsonPropertyName("ids")]
    public List<SteamStoreBrowseId> Ids { get; set; } = new();

    [JsonPropertyName("context")]
    public SteamStoreBrowseContext Context { get; set; } = new();

    [JsonPropertyName("data_request")]
    public SteamStoreBrowseDataRequest DataRequest { get; set; } = new();
}

public sealed class SteamStoreBrowseId
{
    [JsonPropertyName("appid")]
    public uint AppId { get; set; }
}

public sealed class SteamStoreBrowseContext
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "english";

    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = "US";

    [JsonPropertyName("steam_realm")]
    public int SteamRealm { get; set; } = 1;
}

public sealed class SteamStoreBrowseDataRequest
{
    [JsonPropertyName("include_basic_info")]
    public bool IncludeBasicInfo { get; set; }

    [JsonPropertyName("include_release")]
    public bool IncludeRelease { get; set; }

    [JsonPropertyName("include_reviews")]
    public bool IncludeReviews { get; set; }

    [JsonPropertyName("include_all_purchase_options")]
    public bool IncludeAllPurchaseOptions { get; set; }

    [JsonPropertyName("include_assets")]
    public bool IncludeAssets { get; set; }
}

public sealed class SteamStoreBrowseResponseEnvelope
{
    [JsonPropertyName("response")]
    public SteamStoreBrowseResponseBody? Response { get; set; }
}

public sealed class SteamStoreBrowseResponseBody
{
    [JsonPropertyName("store_items")]
    public List<SteamStoreBrowseItem>? StoreItems { get; set; }
}

public sealed class SteamStoreBrowseItem
{
    [JsonPropertyName("appid")]
    public uint AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("basic_info")]
    public SteamStoreBasicInfo? BasicInfo { get; set; }

    [JsonPropertyName("release")]
    public SteamStoreRelease? Release { get; set; }

    [JsonPropertyName("assets")]
    public SteamStoreAssets? Assets { get; set; }
}

/// <summary>
/// Storefront art asset descriptors. The <c>asset_url_format</c> is a
/// <c>${FILENAME}</c> template resolved against a per-filename value in this
/// object; the real files live under the
/// <c>https://shared.akamai.steamstatic.com/store_item_assets/</c> host.
/// Newer apps hash the directory (e.g. <c>{hash}/library_capsule_2x.jpg</c>),
/// so these values MUST be used instead of constructing an appid-based URL.
/// </summary>
public sealed class SteamStoreAssets
{
    [JsonPropertyName("asset_url_format")]
    public string? AssetUrlFormat { get; set; }

    /// <summary>600x900 portrait cover (the app's 2:3 library tile).</summary>
    [JsonPropertyName("library_capsule_2x")]
    public string? LibraryCapsule2x { get; set; }

    [JsonPropertyName("library_capsule")]
    public string? LibraryCapsule { get; set; }
}

public sealed class SteamStoreBasicInfo
{
    [JsonPropertyName("short_description")]
    public string? ShortDescription { get; set; }

    [JsonPropertyName("developers")]
    public List<SteamStoreOwner>? Developers { get; set; }

    [JsonPropertyName("publishers")]
    public List<SteamStoreOwner>? Publishers { get; set; }
}

public sealed class SteamStoreOwner
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class SteamStoreRelease
{
    [JsonPropertyName("steam_release_date")]
    public long SteamReleaseDate { get; set; }
}
