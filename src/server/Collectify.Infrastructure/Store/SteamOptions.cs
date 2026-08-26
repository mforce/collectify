namespace Collectify.Infrastructure.Store;

/// <summary>
/// Steam platform-import configuration. Bound from the "Collectify:Platforms"
/// section (a sibling of Collectify:Metadata, since this is not metadata
/// lookup). Steam is fail-soft like every other provider: if the API key is
/// unset, <see cref="IsConfigured"/> is false and the UI shows a
/// "set the Steam API key to enable" hint instead of wiring the flow.
/// </summary>
public sealed class SteamOptions
{
    public const string SectionName = "Collectify:Platforms";

    public SteamSubOptions Steam { get; set; } = new();

    public sealed class SteamSubOptions
    {
        /// <summary>Steam Web API key (steamcommunity.com/dev/apikey).</summary>
        public string? ApiKey { get; set; }
        /// <summary>OpenID provider base. Hard-coded target for verification.</summary>
        public string OpenIdBaseUrl { get; set; } = "https://steamcommunity.com/openid/login";
        /// <summary>Steam Web API base (endpoint host aliases api + partner).</summary>
        public string ApiBaseUrl { get; set; } = "https://api.steampowered.com/";
        /// <summary>Default TTL for the owner's owned-games cache (short — a private library).</summary>
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
        /// <summary>Max titles a single import may create/select (payload bound).</summary>
        public int ImportCap { get; set; } = 500;
        /// <summary>Max selections a preview may return.</summary>
        public int PreviewCap { get; set; } = 500;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
