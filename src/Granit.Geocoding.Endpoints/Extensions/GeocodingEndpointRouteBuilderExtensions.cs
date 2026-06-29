using Granit.Geocoding.Endpoints.Endpoints;
using Granit.Geocoding.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Geocoding.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping the geocoding HTTP endpoints.
/// </summary>
public static class GeocodingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the geocoding endpoints under <c>/{prefix}</c> — <c>GET /autocomplete</c> and <c>GET /reverse</c> —
    /// each only when the matching capability is present (an autocomplete- / reverse-capable provider is registered).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The group requires <strong>authentication</strong> by default — geocoding proxies an external,
    /// rate-limited, billable provider, so it is never anonymous. Set
    /// <see cref="GeocodingEndpointsOptions.AuthorizationPolicy"/> to require a specific policy.
    /// </para>
    /// <para>
    /// <strong>Rate limiting (strongly recommended).</strong> Autocomplete is per-keystroke and the upstream
    /// provider quota is a shared resource — one principal's spam exhausts the quota / bill for everyone
    /// (denial-of-wallet). Chain <c>.RequireGranitRateLimiting("geocoding")</c> on the returned group with a
    /// <strong>per-principal</strong> policy (<c>RateLimiting:Policies:geocoding</c> partitioned by user / tenant).
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="GeocodingEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining (rate limiting, additional metadata).</returns>
    public static RouteGroupBuilder MapGranitGeocoding(
        this IEndpointRouteBuilder endpoints,
        Action<GeocodingEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        GeocodingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        if (string.IsNullOrEmpty(options.AuthorizationPolicy))
        {
            group.RequireAuthorization();
        }
        else
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        GeocodingCapabilities capabilities = endpoints.ServiceProvider.GetRequiredService<GeocodingCapabilities>();
        group.MapGeocodingEndpoints(capabilities);

        return group;
    }
}
