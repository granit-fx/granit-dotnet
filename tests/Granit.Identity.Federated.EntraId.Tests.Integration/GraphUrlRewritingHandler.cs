namespace Granit.Identity.Federated.EntraId.Tests.Integration;

/// <summary>
/// Rewrites outgoing absolute URLs that would normally hit
/// <c>https://login.microsoftonline.com</c> so they land on the local WireMock
/// server instead.
/// </summary>
/// <remarks>
/// The production <c>EntraIdAdminTokenService</c> builds the token endpoint with
/// <c>EntraIdAdminOptions.GetTokenEndpoint()</c>, which hard-codes
/// <c>login.microsoftonline.com</c>. Because the URL is absolute, the
/// <c>HttpClient.BaseAddress</c> does not apply — a <see cref="DelegatingHandler"/>
/// is the cleanest place to redirect the request at the outer layer of the handler
/// chain. Graph traffic (<c>graph.microsoft.com</c>) is intentionally left alone;
/// those requests already flow through <c>BaseAddress = WireMockUrl</c>.
/// </remarks>
internal sealed class GraphUrlRewritingHandler(Uri wireMockBase) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { Host: "login.microsoftonline.com" } uri)
        {
            UriBuilder rewrite = new(wireMockBase)
            {
                Path = uri.AbsolutePath,
                Query = uri.Query.TrimStart('?'),
            };
            request.RequestUri = rewrite.Uri;
        }
        return base.SendAsync(request, cancellationToken);
    }
}
