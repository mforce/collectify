using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Store;

/// <summary>Response DTOs for the Steam Web API calls used by import.</summary>

public sealed class SteamOwnedGamesResponse
{
    [JsonPropertyName("response")]
    public SteamOwnedGamesBody? Response { get; set; }
}

public sealed class SteamOwnedGamesBody
{
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<SteamOwnedGame>? Games { get; set; }
}

public sealed class SteamOwnedGame
{
    [JsonPropertyName("appid")]
    public uint AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playtime_forever")]
    public long PlaytimeForever { get; set; }

    [JsonPropertyName("img_icon_url")]
    public string? ImgIconUrl { get; set; }
}

/// <summary>
/// Single response wrapper for IPlayerService/GetPlayerSummaries. Only the
/// persona name is consumed (best-effort; a failure here must not fail the
/// whole connect).
/// </summary>
public sealed class SteamPlayerSummariesResponse
{
    [JsonPropertyName("response")]
    public SteamPlayerSummariesBody? Body { get; set; }
}

public sealed class SteamPlayerSummariesBody
{
    [JsonPropertyName("players")]
    public List<SteamPlayerSummary>? Players { get; set; }
}

public sealed class SteamPlayerSummary
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; set; }

    [JsonPropertyName("personaname")]
    public string? PersonaName { get; set; }
}
