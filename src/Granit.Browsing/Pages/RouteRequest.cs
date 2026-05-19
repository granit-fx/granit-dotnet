namespace Granit.Browsing.Pages;

/// <summary>
/// Provider-neutral view of an intercepted request observed by a registered
/// <see cref="RoutePattern"/> handler.
/// </summary>
/// <param name="Url">Fully-qualified request URL.</param>
/// <param name="Method">HTTP method (<c>GET</c>, <c>POST</c>, …).</param>
/// <param name="Headers">Request headers (case-insensitive lookup is the provider's responsibility).</param>
public sealed record RouteRequest(
    Uri Url,
    string Method,
    IReadOnlyDictionary<string, string> Headers);
