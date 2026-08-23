using System.Globalization;
using System.Text.Json;
using Collectify.Domain.Entities;

namespace Collectify.Api.Endpoints;

/// <summary>
/// Builders for <see cref="BulkField{TEntity}"/> setters. Each parser validates
/// its value and returns an error message on failure (never throwing), so the
/// surrounding bulk handler stays atomic: a bad value anywhere in the batch
/// returns 400 and nothing is persisted. Enum parsers mirror the repo's write
/// boundary — only defined members are accepted, undefined values are rejected
/// — so bulk updates cannot bypass the boundary that single writes enforce.
/// </summary>
public static class BulkFieldBuilder
{
    /// <summary>Set a nullable primitive (string, DateOnly, decimal, int, bool, enum).</summary>
    public static BulkField<TEntity> Scalar<TEntity, T>(
        string name, Action<TEntity, T?> set) where TEntity : class, ICollectionEntry
        where T : struct
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                try
                {
                    set(e, el.ValueKind == JsonValueKind.Null
                        ? null
                        : JsonSerializer.Deserialize<T>(el.GetRawText()));
                    return null;
                }
                catch (JsonException)
                {
                    return $"invalid value for {name}.";
                }
            },
        };
    }

    /// <summary>Set a string field (null clears it).</summary>
    public static BulkField<TEntity> Text<TEntity>(
        string name, Action<TEntity, string?> set) where TEntity : class, ICollectionEntry
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                if (el.ValueKind == JsonValueKind.Null) { set(e, null); return null; }
                if (el.ValueKind != JsonValueKind.String) return $"invalid value for {name}.";
                set(e, el.GetString());
                return null;
            },
        };
    }

    /// <summary>Set an enum field, accepting only defined member values (write-boundary parity).</summary>
    public static BulkField<TEntity> Enum<TEntity, TEnum>(
        string name, Action<TEntity, TEnum> set) where TEntity : class, ICollectionEntry
        where TEnum : struct, System.Enum
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                TEnum value;
                try
                {
                    // JsonSerializer.Deserialize<TEnum> with no options only
                    // accepts numeric JSON tokens (the default converter has
                    // no string-name support); a JSON string like "Sold"
                    // needs Enum.Parse instead, matching how every other
                    // enum-typed field in the app (bound via the configured
                    // JsonStringEnumConverter) actually accepts values.
                    value = el.ValueKind == JsonValueKind.String
                        ? System.Enum.Parse<TEnum>(el.GetString()!, ignoreCase: true)
                        : JsonSerializer.Deserialize<TEnum>(el.GetRawText());
                }
                catch (JsonException)
                {
                    return $"invalid value for {name}.";
                }
                catch (ArgumentException)
                {
                    return $"invalid value for {name}.";
                }
                catch (OverflowException)
                {
                    return $"invalid value for {name}.";
                }
                if (!System.Enum.IsDefined(value))
                    return $"'{value}' is not a defined {typeof(TEnum).Name}.";
                set(e, value);
                return null;
            },
        };
    }

    /// <summary>Set a nullable enum field: accepts a defined member name
    /// (case-insensitive, matching the single-write boundary) or JSON null to
    /// clear the value. Rejects undefined member values.</summary>
    public static BulkField<TEntity> NullableEnum<TEntity, TEnum>(
        string name, Action<TEntity, TEnum?> set) where TEntity : class, ICollectionEntry
        where TEnum : struct, System.Enum
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                if (el.ValueKind == JsonValueKind.Null)
                {
                    set(e, null);
                    return null;
                }
                TEnum value;
                try
                {
                    value = el.ValueKind == JsonValueKind.String
                        ? System.Enum.Parse<TEnum>(el.GetString()!, ignoreCase: true)
                        : JsonSerializer.Deserialize<TEnum>(el.GetRawText());
                }
                catch (JsonException)
                {
                    return $"invalid value for {name}.";
                }
                catch (ArgumentException)
                {
                    return $"invalid value for {name}.";
                }
                catch (OverflowException)
                {
                    return $"invalid value for {name}.";
                }
                if (!System.Enum.IsDefined(value))
                    return $"'{value}' is not a defined {typeof(TEnum).Name}.";
                set(e, value);
                return null;
            },
        };
    }

    /// <summary>Set a decimal in [0, 999999.99]: 2 decimal places, never negative.</summary>
    public static BulkField<TEntity> Price<TEntity>(
        string name, Action<TEntity, decimal?> set) where TEntity : class, ICollectionEntry
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                if (el.ValueKind == JsonValueKind.Null) { set(e, null); return null; }
                if (el.ValueKind != JsonValueKind.Number) return $"invalid value for {name}.";
                var raw = el.GetRawText();
                if (!decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var d))
                    return $"invalid value for {name}.";
                if (d < 0) return $"{name} must not be negative.";
                if (decimal.Round(d, 2) != d) return $"{name} must have at most 2 decimal places.";
                set(e, d);
                return null;
            },
        };
    }

    /// <summary>Set a personal rating in [1, 10] (matches the single-write boundary).</summary>
    public static BulkField<TEntity> Rating<TEntity>(
        string name, Action<TEntity, int?> set) where TEntity : class, ICollectionEntry
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                if (el.ValueKind == JsonValueKind.Null) { set(e, null); return null; }
                if (el.ValueKind != JsonValueKind.Number) return $"invalid value for {name}.";
                var raw = el.GetRawText();
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
                    return $"invalid value for {name}.";
                if (r < 1 || r > 10) return $"{name} must be between 1 and 10.";
                set(e, r);
                return null;
            },
        };
    }

    /// <summary>Set a required (non-null) int field, e.g. a count.</summary>
    public static BulkField<TEntity> Count<TEntity>(
        string name, Action<TEntity, int> set) where TEntity : class, ICollectionEntry
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                if (el.ValueKind != JsonValueKind.Number) return $"invalid value for {name}.";
                var raw = el.GetRawText();
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c))
                    return $"invalid value for {name}.";
                if (c < 0) return $"{name} must not be negative.";
                set(e, c);
                return null;
            },
        };
    }

    /// <summary>Set a 3-letter ISO 4217 currency code (null clears it), upper-cased.</summary>
    public static BulkField<TEntity> Currency<TEntity>(
        string name, Action<TEntity, string?> set) where TEntity : class, ICollectionEntry
    {
        return new BulkField<TEntity>
        {
            Name = name,
            Apply = (e, el) =>
            {
                if (el.ValueKind == JsonValueKind.Null) { set(e, null); return null; }
                if (el.ValueKind != JsonValueKind.String) return $"invalid value for {name}.";
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s)) { set(e, null); return null; }
                if (s.Length != 3) return $"{name} must be 3 characters.";
                set(e, s.Trim().ToUpperInvariant());
                return null;
            },
        };
    }
}
