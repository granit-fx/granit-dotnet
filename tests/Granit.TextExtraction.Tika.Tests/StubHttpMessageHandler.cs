using System.Net;

namespace Granit.TextExtraction.Tika.Tests;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that records the inbound request
/// and replies with a caller-supplied response. Avoids pulling WireMock for this PR —
/// keeps the test surface focused on the extractor's transport-layer contract.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    public List<HttpRequestMessage> Calls { get; } = [];

    public static StubHttpMessageHandler RespondWith(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body),
        }));

    public static StubHttpMessageHandler Throws(Exception ex) =>
        new((_, _) => throw ex);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer the body so the test can inspect Content.ReadAsByteArrayAsync after the
        // request is sent (HttpClient closes the original stream on SendAsync).
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        }

        Calls.Add(request);
        return await handler(request, cancellationToken).ConfigureAwait(false);
    }
}
