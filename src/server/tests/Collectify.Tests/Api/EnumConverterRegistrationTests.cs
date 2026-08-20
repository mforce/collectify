using System.Text.Json;
using System.Text.Json.Serialization;
using Collectify.Api.Endpoints;
using Collectify.Domain.Enums;

namespace Collectify.Tests.Api;

/// <summary>
/// Issue #97 (F2) — every enum type that appears as a write-DTO property must
/// have a registered converter in Program.cs's ConfigureHttpJsonOptions block
/// (either a DefinedEnumConverter&lt;T&gt; or, for GamePlatform, the dedicated
/// GamePlatformJsonConverter). This test is the guard: it reflects over the
/// three write DTOs, collects every enum type they expose, and asserts each
/// one is present in a mirror of the Program.cs registration list. The mirror
/// is this test's own oracle (not read from Program.cs) — if a future enum is
/// added to a DTO without a matching registration, this test fails and names
/// the missing type.
/// </summary>
public class EnumConverterRegistrationTests
{
    private static readonly HashSet<Type> RegisteredEnumTypes =
    [
        typeof(GamePlatform),
        typeof(CollectionStatus),
        typeof(Condition),
        typeof(WatchStatus),
        typeof(CompletionStatus),
        typeof(MusicFormat),
    ];

    private static readonly Type[] WriteDtoTypes =
    [
        typeof(MoviesEndpoints.MovieDto),
        typeof(MusicEndpoints.AlbumDto),
        typeof(GamesEndpoints.GameDto),
    ];

    // Mirrors the literal converter registration in Program.cs's
    // ConfigureHttpJsonOptions block. Existence proof that the registration
    // list compiles and wires; the real assertion is REGISTERED-set
    // membership below.
    private static JsonSerializerOptions MirrorOptions()
    {
        var o = new JsonSerializerOptions();
        o.Converters.Add(new GamePlatformJsonConverter());
        o.Converters.Add(new DefinedEnumConverter<CollectionStatus>());
        o.Converters.Add(new DefinedEnumConverter<Condition>());
        o.Converters.Add(new DefinedEnumConverter<WatchStatus>());
        o.Converters.Add(new DefinedEnumConverter<CompletionStatus>());
        o.Converters.Add(new DefinedEnumConverter<MusicFormat>());
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    private static IEnumerable<Type> DiscoverEnumTypes(Type dtoType) =>
        dtoType.GetProperties()
            .Select(p => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)
            .Where(t => t.IsEnum)
            .Distinct();

    [Fact]
    public void MirrorOptions_Compiles()
    {
        var options = MirrorOptions();
        Assert.NotEmpty(options.Converters);
    }

    [Fact]
    public void EveryWriteDtoEnum_HasARegisteredConverter()
    {
        var discoveredEnumTypes = WriteDtoTypes.SelectMany(DiscoverEnumTypes).Distinct();

        foreach (var t in discoveredEnumTypes)
        {
            Assert.True(RegisteredEnumTypes.Contains(t),
                $"Enum '{t.Name}' appears in a write DTO but has no {nameof(DefinedEnumConverter<CollectionStatus>)}<T> or GamePlatformJsonConverter registration. Add it to Program.cs ConfigureHttpJsonOptions AND to this test's mirror.");
        }
    }
}
