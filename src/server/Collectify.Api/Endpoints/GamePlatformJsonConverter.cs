using System.Text.Json;
using System.Text.Json.Serialization;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;

namespace Collectify.Api.Endpoints;

/// <summary>
/// Deserializes <see cref="GamePlatform"/> from JSON while tolerating retired
/// names/values, so a stale client / open pre-upgrade tab posting
/// <c>{ "platform": "Linux" }</c> or the retired integer <c>3</c> lands on
/// <see cref="GamePlatform.Pc"/> instead of 400-ing (#102) — the same graceful
/// degradation the query-param resolver applies.
/// <para>
/// Accepts both the string form ("Pc", "Linux", "steam deck") and the numeric
/// form (1, 3), preserving the prior <c>JsonStringEnumConverter</c> behaviour
/// (the standard <c>PostAsJsonAsync</c> helpers send enums as integers).
/// Live member names/integers bind verbatim; retired values (3, 60) retarget
/// via <see cref="GamePlatformBackfill.RetiredPlatformValues"/>; anything else
/// is rejected with a clear error.
/// </para>
/// </summary>
public sealed class GamePlatformJsonConverter : JsonConverter<GamePlatform>
{
    public override GamePlatform Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Numeric form (e.g. a standard PostAsJsonAsync sends 1 for Pc).
        if (reader.TokenType == JsonTokenType.Number)
        {
            var n = reader.GetInt32();
            // Retired value -> valid replacement (3/Linux -> Pc, 60/Deck -> Pc).
            if (GamePlatformBackfill.RetiredPlatformValues.TryGetValue(n, out var retired))
                return retired;
            if (Enum.IsDefined((GamePlatform)n))
                return (GamePlatform)n;
            throw new JsonException($"Unknown platform value {n}.");
        }

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Platform must be a string or integer.");

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            throw new JsonException("Platform must be a non-empty string.");

        // Exact, DEFINED member name first (Other/Pc/Ps5/...); Enum.IsDefined
        // rejects a retired/unnamed numeric name.
        if (Enum.TryParse<GamePlatform>(raw, ignoreCase: true, out var direct)
            && Enum.IsDefined(direct))
            return direct;

        // Free-text alias fallback (Linux -> Pc, "steam deck" -> Pc, ...).
        if (GamePlatformMapping.TryParse(raw) is { } mapped)
            return mapped;

        throw new JsonException($"Unknown platform '{raw}'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        GamePlatform value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
