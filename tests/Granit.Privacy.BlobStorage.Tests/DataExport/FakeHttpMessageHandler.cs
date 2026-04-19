using System.Net;

namespace Granit.Privacy.BlobStorage.Tests.DataExport;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> stub that maps request URLs to canned responses.
/// PUT bodies are buffered into <see cref="CapturedUploads"/> before the production code
/// disposes the underlying <c>StreamContent</c>, so tests can still inspect the archive bytes.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _map = new(StringComparer.Ordinal);

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<CapturedUpload> CapturedUploads { get; } = [];

    public void MapGet(Uri url, byte[] payload, string contentType = "application/json") =>
        _map[url.ToString()] = _ =>
        {
            ByteArrayContent content = new(payload);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        };

    public void MapPut(Uri url) =>
        _map[url.ToString()] = _ => new HttpResponseMessage(HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (request.Method == HttpMethod.Put && request.Content is not null)
        {
            byte[] body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            CapturedUploads.Add(new CapturedUpload(request.RequestUri!, body));
        }

        string key = request.RequestUri!.ToString();
        if (_map.TryGetValue(key, out Func<HttpRequestMessage, HttpResponseMessage>? factory))
        {
            return factory(request);
        }

        if (request.Method == HttpMethod.Put)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

internal sealed record CapturedUpload(Uri Url, byte[] Body);
