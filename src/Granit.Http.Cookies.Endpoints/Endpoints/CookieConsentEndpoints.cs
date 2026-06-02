using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Http.Cookies.Endpoints.Endpoints;

/// <summary>
/// Minimal API handlers for the cookie consent configuration endpoint.
/// </summary>
internal static class CookieConsentEndpoints
{
    /// <summary>Maps the cookie consent endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapCookieConsentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/config", HandleGetConfig)
            .AllowAnonymous()
            .WithName("GetCookieConsentConfig")
            .WithSummary("Returns the cookie consent configuration for CMP setup.")
            .WithDescription("Returns the full list of internal cookies and third-party services registered in the application, categorized for GDPR consent management. The front-end CMP (Consent Management Platform) uses this response to render the cookie banner. Response is cached for 1 hour (Cache-Control: public, max-age=3600). Anonymous — no authentication required.")
            .Produces<CookieConsentConfigResponse>();

        return group;
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
