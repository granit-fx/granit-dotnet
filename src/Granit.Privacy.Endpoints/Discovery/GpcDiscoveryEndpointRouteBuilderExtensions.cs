using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Endpoints.Discovery;

/// <summary>
/// Extension methods for the Global Privacy Control public discovery endpoint.
/// </summary>
public static class GpcDiscoveryEndpointRouteBuilderExtensions
{
    private const string DiscoveryPath = "/.well-known/gpc.json";

    /// <summary>
    /// Maps <c>GET /.well-known/gpc.json</c> at the host root, serving the GPC
    /// discovery document. No-op when <see cref="GpcDiscoveryOptions.Enabled"/> is
    /// <see langword="false"/> — the path then returns 404 naturally, which is the
    /// behavior the GPC spec expects for non-publishers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The endpoint is anonymous, excluded from OpenAPI, and emits a
    /// <c>Cache-Control: public, max-age=&lt;n&gt;</c> header per
    /// <see cref="GpcDiscoveryOptions.CacheMaxAgeSeconds"/>.
    /// </para>
    /// <para>
    /// Intentionally NOT mapped under the privacy route group — the
    /// <c>.well-known</c> URI scheme (RFC 8615) requires host-root placement, and
    /// the resource must be reachable without authentication and outside any API
    /// versioning prefix.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapGranitGpcDiscovery(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        GpcDiscoveryOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<GpcDiscoveryOptions>>()
            .Value;

        if (!options.Enabled)
        {
            return endpoints;
        }

        // Validator guarantees LastUpdate is set when Enabled.
        DateOnly lastUpdate = options.LastUpdate!.Value;
        string cacheControl = $"public, max-age={options.CacheMaxAgeSeconds}";
        GpcDiscoveryDocument document = new(Gpc: true, LastUpdate: lastUpdate);

        endpoints.MapGet(DiscoveryPath, (HttpContext httpContext) =>
            {
                httpContext.Response.Headers.CacheControl = cacheControl;
                return TypedResults.Json(document, contentType: "application/json");
            })
            .AllowAnonymous()
            .ExcludeFromDescription()
            .WithName("GetGpcDiscovery");

        return endpoints;
    }
}
