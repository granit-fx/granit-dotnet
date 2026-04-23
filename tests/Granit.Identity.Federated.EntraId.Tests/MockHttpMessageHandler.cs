using System.Net;

namespace Granit.Identity.Federated.EntraId.Tests;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that captures outgoing requests
/// and returns a configurable response.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    /// <summary>Captured requests as (Method, Url, Body) tuples.</summary>
    public List<(string Method, string Url, string Body)> Requests { get; } = [];

    /// <summary>Status code returned by all responses. Defaults to <see cref="HttpStatusCode.OK"/>.</summary>
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>Response body returned by all responses. Defaults to empty.</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;

        Requests.Add((request.Method.Method, request.RequestUri?.ToString() ?? "", body));

        return new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(ResponseBody, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>
/// Handler that returns different responses for sequential requests.
/// </summary>
internal sealed class MockSequenceHttpMessageHandler(IReadOnlyList<string> responses) : HttpMessageHandler
{
    private int _callIndex;

    /// <summary>Number of HTTP calls made through this handler.</summary>
    public int CallCount => _callIndex;

    /// <summary>Captured requests as (Method, Url, Body) tuples — in the order sent.</summary>
    public List<(string Method, string Url, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;

        Requests.Add((request.Method.Method, request.RequestUri?.ToString() ?? "", body));

        int index = Math.Min(_callIndex++, responses.Count - 1);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responses[index], System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
