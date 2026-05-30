namespace Collectify.Infrastructure.Lookup.Vision;

/// <summary>Scored web entity from WEB_DETECTION.</summary>
public record WebEntitySignal(string Description, float Score);

/// <summary>Categorised matching URL from WEB_DETECTION.</summary>
/// <remarks>Category: "pagesWithMatchingImages" | "fullMatch" | "partialMatch" | "visuallySimilar"</remarks>
public record MatchingUrlSignal(Uri Uri, string Category);

/// <summary>
/// Multi-signal result from analysing a cover photo. Callers combine
/// these signals into ranked queries for the metadata provider.
/// </summary>
public record VisionExtractResult(
    string[] DetectedText,
    float TextConfidence,
    WebEntitySignal[] WebEntities,
    MatchingUrlSignal[] MatchingUrls);

public interface IVisionClient
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<VisionExtractResult> AnalyseAsync(byte[] imageBytes, CancellationToken ct = default);
}
