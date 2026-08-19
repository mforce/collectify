using System.Text.Json;
using System.Text.Json.Serialization;

namespace Collectify.Api.Endpoints;

/// <summary>
/// A <see cref="JsonConverter{T}"/> for a plain (non-<c>[Flags]</c>) enum that
/// accepts the member whether it arrives as its string name or as its numeric
/// value, but **rejects** a value that is not a defined member — closing the
/// write-boundary gap where the default <c>JsonStringEnumConverter</c> (with
/// <c>allowIntegerValues: true</c>) would accept an arbitrary integer (e.g.
/// <c>999</c>, or a retired platform value) and persist an unnamed enum value
/// that the startup backfill then has to paper over.
///
/// Semantics are deliberately conservative to preserve the existing wire
/// contract: a *defined* integer (the form <c>PostAsJsonAsync</c> and older
/// clients send) still binds, exactly as it did before. What is newly rejected
/// is an integer that corresponds to no declared member. Strings must name a
/// defined member (the pre-existing behaviour of <c>JsonStringEnumConverter</c>
/// for a bad string already threw).
///
/// <c>[Flags]</c> enums are NOT handled here — they carry bitmask combinations
/// (e.g. <c>MovieFormats</c> is bound as an <c>int</c> and validated separately).
/// Register one instance per write-boundary enum in <c>Program.cs</c>, BEFORE
/// the global <c>JsonStringEnumConverter</c> so it wins for its type.
/// </summary>
public sealed class DefinedEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var n = reader.GetInt32();
            if (Enum.IsDefined(typeof(TEnum), n))
                return (TEnum)Enum.ToObject(typeof(TEnum), n);
            throw new JsonException($"Unknown {typeof(TEnum).Name} value {n}.");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (!string.IsNullOrWhiteSpace(raw)
                && Enum.TryParse(typeof(TEnum), raw, ignoreCase: true, out var parsed)
                && parsed is TEnum value
                && Enum.IsDefined(typeof(TEnum), value))
            {
                return value;
            }
            throw new JsonException($"Unknown {typeof(TEnum).Name} value '{raw}'.");
        }

        throw new JsonException($"{typeof(TEnum).Name} must be a string or integer.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
