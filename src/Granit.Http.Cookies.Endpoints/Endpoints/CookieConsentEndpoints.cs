using System.Diagnostics;
using Granit.Http.Cookies.Domain;
using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Internal;
using Granit.Http.Cookies.Ledger;
using Granit.Http.Idempotency;
using Granit.Http.RateLimiting.AspNetCore;
using Granit.Http.UrlSafety;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Http.Cookies.Endpoints.Endpoints;

/// <summary>
/// Minimal API handlers for the cookie consent endpoints.
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

        group.MapPost("/consent", RecordConsentAsync)
            .AllowAnonymous()
            .WithName("RecordCookieConsentDecision")
            .WithSummary("Records a cookie-consent decision in the server-side ledger.")
            .WithDescription("Appends the user's consent decision (granted and denied categories) to the append-only consent ledger — the GDPR Art. 7(1) evidence that consent was given. Called by the front-end CMP after the user interacts with the cookie banner, before any authentication. The client IP is irreversibly anonymized and the user-agent truncated before storage; without a persistent ledger registered the decision is acknowledged but not recorded.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireGranitRateLimiting(CookieConsentRateLimitPolicies.Record);

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

    private static async Task<NoContent> RecordConsentAsync(
        ConsentDecisionRequest request,
        [FromServices] IConsentLedger ledger,
        [FromServices] TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // Data minimisation at capture (GDPR Art. 5(1)(c)): irreversible IP masking;
        // the factory truncates the user-agent — the ledger never sees a raw identifier.
        string? anonymizedIp = IpAddressAnonymizer.Mask(context.Connection.RemoteIpAddress);
        string userAgentHeader = context.Request.Headers.UserAgent.ToString();

        var record = CookieConsentRecord.Create(
            request.GrantedCategories ?? [],
            request.DeniedCategories ?? [],
            // The CMP reports the model it enforced; absent, assume OptIn (GDPR-safe default).
            request.Mode ?? CookieConsentMode.OptIn,
            request.CmpSource,
            timeProvider.GetUtcNow(),
            anonymizedIp,
            userAgentHeader.Length > 0 ? userAgentHeader : null,
            Activity.Current?.Id);

        await ledger.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        // Acknowledgment without a body: the CMP needs no payload back.
        return TypedResults.NoContent();
    }
}
