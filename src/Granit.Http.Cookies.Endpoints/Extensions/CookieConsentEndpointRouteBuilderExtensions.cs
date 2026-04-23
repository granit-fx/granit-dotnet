using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Internal;
using Granit.Http.Cookies.Endpoints.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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
    public static IEndpointRouteBuilder MapGranitCookieConsent(
        this IEndpointRouteBuilder endpoints,
        Action<CookieConsentEndpointsOptions>? configure = null)
    {
        CookieConsentEndpointsOptions options = new();
        configure?.Invoke(options);

        endpoints
            .MapGet($"{options.RoutePrefix}/config", HandleGetConfig)
            .AllowAnonymous()
            .WithName("GetCookieConsentConfig")
            .WithTags(options.TagName)
            .WithSummary("Returns the cookie consent configuration for CMP setup.")
            .WithDescription("Returns the full list of internal cookies and third-party services registered in the application, categorized for GDPR consent management. The front-end CMP (Consent Management Platform) uses this response to render the cookie banner. Response is cached for 1 hour (Cache-Control: public, max-age=3600). Anonymous — no authentication required.")
            .Produces<CookieConsentConfigResponse>();

        return endpoints;
    }

    private static Ok<CookieConsentConfigResponse> HandleGetConfig(
        [FromServices] ICookieRegistry cookieRegistry,
        [FromServices] IThirdPartyServiceRegistry serviceRegistry,
        HttpContext context)
    {
        context.Response.Headers.CacheControl = "public, max-age=3600";

        CookieConsentConfigProvider provider = new(cookieRegistry, serviceRegistry);
        return TypedResults.Ok(provider.GetConfig());
    }

}
