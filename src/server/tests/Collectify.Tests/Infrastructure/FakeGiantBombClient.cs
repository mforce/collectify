using Collectify.Infrastructure.Lookup.GiantBomb;
using Collectify.Infrastructure.Lookup.Upc;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="IGiantBombGameUpcClient"/>. Mirrors
/// <see cref="FakeUpcClient"/> shape so tests can dial in
/// "configured-but-no-hit" vs "configured-with-hit" vs the default
/// "not configured at all" paths.
/// </summary>
public sealed class FakeGiantBombClient : IGiantBombGameUpcClient
{
    public string Name => "fake-giantbomb";
    public bool IsConfigured { get; init; }
    public List<string> RequestedBarcodes { get; } = new();
    public UpcLookupResult? Result { get; init; }

    public Task<UpcLookupResult?> LookupAsync(string barcode, CancellationToken ct = default)
    {
        if (!IsConfigured) return Task.FromResult<UpcLookupResult?>(null);
        RequestedBarcodes.Add(barcode);
        return Task.FromResult(Result);
    }

    public static FakeGiantBombClient NotConfigured() => new() { IsConfigured = false };

    public static FakeGiantBombClient ConfiguredNotRecognised() =>
        new() { IsConfigured = true };

    public static FakeGiantBombClient Returning(string title) =>
        new() { IsConfigured = true, Result = new UpcLookupResult(title, null, null) };
}
