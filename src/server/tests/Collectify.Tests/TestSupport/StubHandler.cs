using System.Net;
using System.Text;

namespace Collectify.Tests.TestSupport;

/// <summary>
/// A controller-style <see cref="HttpMessageHandler"/> that returns a fixed
/// body/status for every request and records the request URIs it served.
/// Providers differ only by the response they return, so the four metadata
/// client/provider test classes that each used to declare a private copy of
/// this handler now share this one.
/// </summary>
/// <remarks>
/// Deliberately the minimal identical shape only. Handlers with richer capture
/// (Igdb's <c>CapturedRequest</c>), per-URL routing (Tmdb's
/// <c>RoutingStubHandler</c>), a response sequence (<c>SequenceHandler</c>), a
/// login/response-building body (<c>SteamOpenIdVerifierTests</c>) or a binary
/// payload (<c>CoverImageStoreTests</c>) each kept their own implementation —
/// they are not this shape.
/// </remarks>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly string _body;
    private readonly HttpStatusCode _status;

    public List<string> RequestedUrls { get; } = new();

    public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _body = body;
        _status = status;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // AbsoluteUri keeps percent-encoding intact; ToString unescapes %20 -> space.
        RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json"),
        });
    }
}
