using System.Net;
using System.Text;

namespace Granit.Invoicing.Odoo.Tests;

/// <summary>
/// Sequenced HTTP test double: returns response bodies from <see cref="ResponseQueue"/>
/// in FIFO order; falls back to <see cref="DefaultResponseBody"/> when the queue is empty.
/// Captures request bodies for assertion on JSON-RPC payloads.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    public List<(string Method, string Url, string Body)> Requests { get; } = [];
    public Queue<string> ResponseQueue { get; } = new();
    public string? DefaultResponseBody { get; set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;
        Requests.Add((request.Method.Method, request.RequestUri?.PathAndQuery ?? "", body));

        string responseBody = ResponseQueue.Count > 0 ? ResponseQueue.Dequeue() : (DefaultResponseBody ?? "{}");
        var response = new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
        return response;
    }
}
