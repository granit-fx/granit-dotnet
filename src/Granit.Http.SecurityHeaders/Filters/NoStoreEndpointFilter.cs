using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Granit.Http.SecurityHeaders.Filters;

/// <summary>
/// Endpoint filter that prevents an endpoint's response from being stored by
/// browsers, proxies, or CDN caches. Use on endpoints that emit secrets,
/// short-lived tokens, or other content that must never appear in a cache.
/// </summary>
/// <remarks>
/// <para>
/// Sets all three cache-suppression headers so HTTP/1.0 intermediaries (older
/// corporate proxies, some load balancers) that ignore <c>Cache-Control</c>
/// still bypass their caches:
/// </para>
/// <list type="bullet">
///   <item><c>Cache-Control: no-store, no-cache, must-revalidate, max-age=0</c> (HTTP/1.1)</item>
///   <item><c>Pragma: no-cache</c> (HTTP/1.0 fallback)</item>
///   <item><c>Expires: 0</c> (HTTP/1.0 fallback — RFC 7234 §5.3 treats invalid dates as expired)</item>
/// </list>
/// <para>
/// The headers are only set on success responses (2xx). Error responses keep
/// their default cache headers so clients may apply normal retry semantics.
/// </para>
/// </remarks>
public sealed class NoStoreEndpointFilter : IEndpointFilter
{
    private const string CacheControlValue = "no-store, no-cache, must-revalidate, max-age=0";
    private const string PragmaValue = "no-cache";
    private const string ExpiresValue = "0";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        object? result = await next(context).ConfigureAwait(false);

        HttpResponse response = context.HttpContext.Response;
        if (response.StatusCode is >= 200 and < 300)
        {
            response.Headers[HeaderNames.CacheControl] = new StringValues(CacheControlValue);
            response.Headers[HeaderNames.Pragma] = new StringValues(PragmaValue);
            response.Headers[HeaderNames.Expires] = new StringValues(ExpiresValue);
        }

        return result;
    }
}
