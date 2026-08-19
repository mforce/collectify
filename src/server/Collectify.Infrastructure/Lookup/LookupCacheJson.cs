using System.Text.Json;
using System.Text.Json.Serialization;

namespace Collectify.Infrastructure.Lookup;

public static class LookupCacheJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) },
    };
}
