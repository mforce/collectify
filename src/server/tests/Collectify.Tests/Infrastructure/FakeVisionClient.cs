using Collectify.Infrastructure.Lookup.Vision;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Test double for IVisionClient. Returns whatever the test scripted
/// via init-only properties so endpoint tests can compose deterministic
/// vision responses without hitting Google Cloud.
/// </summary>
public sealed class FakeVisionClient : IVisionClient
{
    public string Name { get; init; } = "fake-vision";
    public bool IsConfigured { get; init; } = true;
    public string[] DetectedText { get; init; } = [];
    public float TextConfidence { get; init; } = 1f;
    public WebEntitySignal[] WebEntities { get; init; } = [];
    public MatchingUrlSignal[] MatchingUrls { get; init; } = [];

    public Task<VisionExtractResult> AnalyseAsync(byte[] imageBytes, CancellationToken ct = default)
        => Task.FromResult(new VisionExtractResult(
            DetectedText, TextConfidence, WebEntities, MatchingUrls));

    public static FakeVisionClient WithText(params string[] words) =>
        new() { DetectedText = words };

    public static FakeVisionClient WithEntities(params WebEntitySignal[] entities) =>
        new() { WebEntities = entities };

    public static FakeVisionClient WithUrls(params MatchingUrlSignal[] urls) =>
        new() { MatchingUrls = urls };

    public static FakeVisionClient NotConfigured() =>
        new() { IsConfigured = false };
}
