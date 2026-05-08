using Collectify.Infrastructure.Lookup.Upc;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="IUpcLookupClient"/>. Returns whatever the
/// test scripted (default: null = "barcode not recognised") and records
/// the codes it was called with so assertions can verify dispatch.
/// </summary>
public sealed class FakeUpcClient : IUpcLookupClient
{
    public string Name => "fake-upc";
    public List<string> RequestedBarcodes { get; } = new();
    public UpcLookupResult? Result { get; init; }

    public Task<UpcLookupResult?> LookupAsync(string barcode, CancellationToken ct = default)
    {
        RequestedBarcodes.Add(barcode);
        return Task.FromResult(Result);
    }

    public static FakeUpcClient Returning(string title, string? brand = null, string? manufacturer = null) =>
        new() { Result = new UpcLookupResult(title, brand, manufacturer) };

    public static FakeUpcClient NotRecognised() => new();
}
