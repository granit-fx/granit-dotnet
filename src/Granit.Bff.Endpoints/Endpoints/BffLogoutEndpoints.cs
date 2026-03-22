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
/// </summary>
internal static partial class BffLogoutEndpoints
{
    internal static RouteGroupBuilder MapLogoutEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/logout", HandleLogoutAsync)
            .WithName("BffLogout")
            .WithSummary("Logs out the user, clears the session, and redirects to the OIDC end_session_endpoint.")
            .WithDescription(
                "Removes the token set from the distributed cache, deletes the session cookie, "
                + "and redirects to the OIDC provider's end_session_endpoint for RP-Initiated Logout. "
                + "If no session exists, redirects to the post-logout path directly.")
            .Produces(StatusCodes.Status302Found)
            .ExcludeFromDescription();

        return group;
    }

    private static async Task<RedirectHttpResult> HandleLogoutAsync(
        HttpContext httpContext,
        [FromServices] IOptions<GranitBffOptions> options,
        [FromServices] IBffTokenStore tokenStore,
        [FromServices] BffMetrics metrics,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Bff.Endpoints.BffLogoutEndpoints");
        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Logout);
        using IDisposable? activityScope = activity;

        GranitBffOptions bffOptions = options.Value;
        string? sessionId = httpContext.Request.Cookies[bffOptions.SessionCookieName];

        string? idTokenHint = null;

        if (!string.IsNullOrEmpty(sessionId))
        {
#pragma warning disable GRSEC003 // Variable handles tokens — server-side only
            BffTokenSet? tokens = await tokenStore.GetAsync(sessionId, cancellationToken)
                .ConfigureAwait(false);
            idTokenHint = tokens?.IdToken;
#pragma warning restore GRSEC003

            await tokenStore.RemoveAsync(sessionId, cancellationToken).ConfigureAwait(false);
            metrics.RecordLogout(null);
            LogLogout(logger, sessionId);
        }

        // Clear session cookie
        httpContext.Response.Cookies.Delete(bffOptions.SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

        // Build end_session URL
        string postLogoutRedirectUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{bffOptions.PostLogoutRedirectPath}";
        string endSessionUrl = $"{bffOptions.Authority.ToString().TrimEnd('/')}/connect/endsession"
            + $"?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}"
            + $"&client_id={Uri.EscapeDataString(bffOptions.ClientId)}";

        if (!string.IsNullOrEmpty(idTokenHint))
        {
#pragma warning disable GRSEC003 // Appending id_token_hint — standard OIDC parameter
            endSessionUrl += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";
#pragma warning restore GRSEC003
        }

        return TypedResults.Redirect(endSessionUrl);
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF logout: session {SessionId} cleared")]
    private static partial void LogLogout(ILogger logger, string sessionId);
}
