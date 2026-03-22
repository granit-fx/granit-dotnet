using System.Diagnostics;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF logout endpoint. Clears the session and redirects to the OIDC end_session_endpoint.
/// Registered per-frontend under <c>/{pathPrefix}/bff/logout</c>.
/// </summary>
internal static partial class BffLogoutEndpoints
{
    internal static RouteGroupBuilder MapLogoutEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapGet("/logout", (HttpContext httpContext,
                [FromServices] IOptions<GranitBffOptions> options,
                [FromServices] IBffTokenStore tokenStore,
                [FromServices] BffMetrics metrics,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                HandleLogoutAsync(httpContext, frontend, options, tokenStore, metrics, loggerFactory, cancellationToken))
            .WithName($"BffLogout_{frontend.Name}")
            .WithSummary("Logs out the user, clears the session, and redirects to the OIDC end_session_endpoint.")
            .WithDescription(
                "Removes the token set from the distributed cache, deletes the session cookie, "
                + "and redirects to the OIDC provider's end_session_endpoint for RP-Initiated Logout. "
                + "If no session exists, redirects to the post-logout path directly.")
            .Produces(StatusCodes.Status302Found)
            .ExcludeFromDescription();

        return group;
    }

#pragma warning disable GRAPI003 // Private handler — not a direct endpoint delegate; services are resolved via lambda
    private static async Task<RedirectHttpResult> HandleLogoutAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        IOptions<GranitBffOptions> options,
        IBffTokenStore tokenStore,
        BffMetrics metrics,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Bff.Endpoints.BffLogoutEndpoints");
        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Logout);
        using IDisposable? activityScope = activity;

        GranitBffOptions bffOptions = options.Value;
        string? sessionId = httpContext.Request.Cookies[frontend.SessionCookieName];

        string? idTokenHint = null;

        if (!string.IsNullOrEmpty(sessionId))
        {
#pragma warning disable GRSEC003 // Variable handles tokens — server-side only
            BffTokenSet? tokens = await tokenStore.GetAsync(frontend.Name, sessionId, cancellationToken)
                .ConfigureAwait(false);
            idTokenHint = tokens?.IdToken;
#pragma warning restore GRSEC003

            await tokenStore.RemoveAsync(frontend.Name, sessionId, cancellationToken).ConfigureAwait(false);
            metrics.RecordLogout(null);
            LogLogout(logger, sessionId, frontend.Name);
        }

        // Clear frontend-specific session cookie
        httpContext.Response.Cookies.Delete(frontend.SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

        // Build end_session URL
#pragma warning disable GRSEC003 // Building OIDC end_session URL with client credentials
        string postLogoutRedirectUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{frontend.EffectivePostLogoutRedirectPath}";
        string endSessionUrl = $"{bffOptions.Authority.ToString().TrimEnd('/')}/connect/endsession"
            + $"?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}"
            + $"&client_id={Uri.EscapeDataString(frontend.ClientId)}";
#pragma warning restore GRSEC003

        if (!string.IsNullOrEmpty(idTokenHint))
        {
#pragma warning disable GRSEC003 // Appending id_token_hint — standard OIDC parameter
            endSessionUrl += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";
#pragma warning restore GRSEC003
        }

        return TypedResults.Redirect(endSessionUrl);
    }
#pragma warning restore GRAPI003

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF logout: session {SessionId} cleared for frontend {FrontendName}")]
    private static partial void LogLogout(ILogger logger, string sessionId, string frontendName);
}
