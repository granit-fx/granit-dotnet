using System.Diagnostics;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Http.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF logout endpoint. Clears the session and redirects to the OIDC end_session_endpoint.
/// Registered per-frontend under <c>/{pathPrefix}/bff/logout</c>.
/// </summary>
internal static class BffLogoutEndpoints
{
    internal static RouteGroupBuilder MapLogoutEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapGet("/logout", (HttpContext httpContext,
                [FromServices] IBffLogoutOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
                HandleLogoutAsync(httpContext, frontend, orchestrator, cancellationToken))
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
        IBffLogoutOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Logout);
        using IDisposable? activityScope = activity;

        string? sessionId = httpContext.Request.Cookies[frontend.SessionCookieName];
        string? idTokenHint = null;

        if (!string.IsNullOrEmpty(sessionId))
        {
            idTokenHint = await orchestrator.RevokeSessionAsync(
                frontend.Name, sessionId, cancellationToken).ConfigureAwait(false);
        }

        // Clear frontend-specific session cookie via managed cookie system (GRSEC004)
        IGranitCookieManager cookieManager = httpContext.RequestServices.GetRequiredService<IGranitCookieManager>();
        cookieManager.DeleteCookie(httpContext, frontend.SessionCookieName);

        // Build end_session URL
        string postLogoutRedirectUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{frontend.EffectivePostLogoutRedirectPath}";
        string endSessionUrl = orchestrator.BuildEndSessionUrl(frontend, postLogoutRedirectUri, idTokenHint);

        return TypedResults.Redirect(endSessionUrl);
    }
#pragma warning restore GRAPI003
}
