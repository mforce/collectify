using System.Net;
using System.Text;

namespace Collectify.Tests.TestSupport;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that returns a fixed body/status for
/// every request and records the request URIs it served.
/// </summary>
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
