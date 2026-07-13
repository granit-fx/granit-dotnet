using Granit.Http.Cookies.Endpoints.Endpoints;
using Granit.Http.Cookies.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.Endpoints.Extensions;

/// <summary>
/// Extension methods to map cookie consent endpoints on <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class CookieConsentEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>GET /cookies/config</c> — returns the full cookie consent configuration
    /// (internal cookies + third-party services) for CMP setup.
    /// Anonymous endpoint, cacheable.
    /// </summary>
    public static RouteGroupBuilder MapGranitCookieConsent(
        this IEndpointRouteBuilder endpoints,
        Action<CookieConsentEndpointsOptions>? configure = null)
    {
        // Bound from Http:Cookies:Endpoints by the module; the delegate overrides on top.
        CookieConsentEndpointsOptions options =
            endpoints.ServiceProvider.GetService<IOptions<CookieConsentEndpointsOptions>>()?.Value ?? new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapCookieConsentEndpoints();

        return group;
    }
}
