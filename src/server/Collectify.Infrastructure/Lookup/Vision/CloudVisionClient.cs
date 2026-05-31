using System.Net.Http.Json;
using Collectify.Infrastructure.Lookup.Vision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Vision;

/// <summary>
/// Google Cloud Vision API client that sends an image and returns
/// multi-signal analysis: OCR text, web entities, and matching URLs.
/// </summary>
public sealed class CloudVisionClient : IVisionClient
{
    private readonly HttpClient _http;
    private readonly MetadataLookupOptions _options;
    private readonly ILogger<CloudVisionClient> _log;

    public CloudVisionClient(
        HttpClient http,
        IOptions<MetadataLookupOptions> options,
        ILogger<CloudVisionClient> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public string Name => "google-vision";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Vision.ApiKey);

    public async Task<VisionExtractResult> AnalyseAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new VisionExtractResult([], 0f, [], []);

        var base64 = Convert.ToBase64String(imageBytes);
        var body = new
        {
            requests = new[]
            {
                new
                {
                    image = new { content = base64 },
                    features = new[]
                    {
                        new { type = "TEXT_DETECTION" },
                        new { type = "WEB_DETECTION" }
                    }
                }
            }
        };

        var url = $"{_options.Vision.BaseUrl}images:annotate?key={_options.Vision.ApiKey}";

        try
        {
            var response = await _http.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CloudVisionResponse>();
            var json = await response.Content.ReadAsStringAsync(ct);
            if (result?.Responses is null || result.Responses.Length == 0)
                return new VisionExtractResult([], 0f, [], []);

            var resp = result.Responses[0];
            return MapResponse(resp);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cloud Vision API call failed; returning empty result.");
            return new VisionExtractResult([], 0f, [], []);
        }
    }

    private static VisionExtractResult MapResponse(CloudVisionResultResponse resp)
    {
        // Map text annotations — skip index 0 (full text blob), filter by length.
        string[] detectedText = [];
        float textConfidence = 0f;
        if (resp.TextAnnotations is not null && resp.TextAnnotations.Length > 1)
        {
            var texts = resp.TextAnnotations[1..]
                .Where(a => a.Description is not null && a.Description.Length >= 2 && a.Description.Length <= 60)
                .Select(a => a.Description!)
                .ToArray();
            detectedText = texts;
            if (texts.Length > 0)
            {
                textConfidence = (float)resp.TextAnnotations[1..]
                    .Where(a => a.Description is not null && a.Description.Length >= 2 && a.Description.Length <= 60)
                    .Average(a => a.Confidence ?? 0f);
            }
        }

        // Map web entities.
        WebEntitySignal[] webEntities = [];
        if (resp.WebDetection?.WebEntities is not null)
        {
            webEntities = resp.WebDetection.WebEntities
                .Select(e => new WebEntitySignal(e.Description, (float)(e.Score ?? 0)))
                .ToArray();
        }

        // Map matching URLs with categories.
        var matchingUrls = new List<MatchingUrlSignal>();

        if (resp.WebDetection is not null)
        {
            if (resp.WebDetection.PagesWithMatchingImages is not null)
            {
                foreach (var page in resp.WebDetection.PagesWithMatchingImages)
                {
                    if (Uri.TryCreate(page.Url, UriKind.Absolute, out var uri))
                        matchingUrls.Add(new MatchingUrlSignal(uri, "pagesWithMatchingImages"));
                }
            }

            if (resp.WebDetection.FullMatchingImageUrls is not null)
            {
                foreach (var img in resp.WebDetection.FullMatchingImageUrls)
                {
                    if (Uri.TryCreate(img.Url, UriKind.Absolute, out var uri))
                        matchingUrls.Add(new MatchingUrlSignal(uri, "fullMatch"));
                }
            }

            if (resp.WebDetection.PartialMatchingImageUrls is not null)
            {
                foreach (var img in resp.WebDetection.PartialMatchingImageUrls)
                {
                    if (Uri.TryCreate(img.Url, UriKind.Absolute, out var uri))
                        matchingUrls.Add(new MatchingUrlSignal(uri, "partialMatch"));
                }
            }

            if (resp.WebDetection.VisuallySimilarImages is not null)
            {
                foreach (var img in resp.WebDetection.VisuallySimilarImages)
                {
                    if (Uri.TryCreate(img.Url, UriKind.Absolute, out var uri))
                        matchingUrls.Add(new MatchingUrlSignal(uri, "visuallySimilar"));
                }
            }
        }

        return new VisionExtractResult(detectedText, textConfidence, webEntities, [.. matchingUrls]);
    }

    // --- Minimal response DTOs ---

    private sealed class CloudVisionResponse
    {
        public CloudVisionResultResponse[]? Responses { get; set; }
    }

    private sealed class CloudVisionResultResponse
    {
        public TextAnnotation[]? TextAnnotations { get; set; }
        public WebDetection? WebDetection { get; set; }
    }

    private sealed class TextAnnotation
    {
        public string? Description { get; set; }
        public double? Confidence { get; set; }
    }

    private sealed class WebDetection
    {
        public WebEntity[]? WebEntities { get; set; }
        public PageWithMatchingImage[]? PagesWithMatchingImages { get; set; }
        public VisuallySimilarImage[]? FullMatchingImageUrls { get; set; }
        public VisuallySimilarImage[]? PartialMatchingImageUrls { get; set; }
        public VisuallySimilarImage[]? VisuallySimilarImages { get; set; }
    }

    private sealed class WebEntity
    {
        public string Description { get; set; } = "";
        public double? Score { get; set; }
    }

    private sealed class PageWithMatchingImage
    {
        public string Url { get; set; } = "";
    }

    private sealed class VisuallySimilarImage
    {
        public string Url { get; set; } = "";
    }
}
