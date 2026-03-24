using System.Diagnostics;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
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
                [FromServices] IHttpClientFactory httpClientFactory,
                [FromServices] IDPoPProofService dpopService,
                [FromServices] IClock clock,
                [FromServices] BffMetrics metrics,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                HandleLogoutAsync(httpContext, frontend, options, tokenStore, httpClientFactory,
                    dpopService, clock, metrics, loggerFactory, cancellationToken))
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
        IHttpClientFactory httpClientFactory,
        IDPoPProofService dpopService,
        IClock clock,
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

            // Revoke tokens at the authorization server (RFC 7009) — best-effort
            if (tokens is not null)
            {
                await RevokeTokensAsync(
                    httpClientFactory, bffOptions, frontend, tokens,
                    clock, dpopService, logger, cancellationToken).ConfigureAwait(false);
            }
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

    /// <summary>
    /// Best-effort token revocation (RFC 7009). Revokes refresh token first (cascades
    /// in OpenIddict), then access token. Failures are logged but do not block logout.
    /// </summary>
#pragma warning disable GRSEC003 // Method handles tokens for revocation — server-side only
    private static async Task RevokeTokensAsync(
        IHttpClientFactory httpClientFactory,
        GranitBffOptions bffOptions,
        BffFrontendOptions frontend,
        BffTokenSet tokens,
        IClock clock,
        IDPoPProofService dpopService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
            string revokeEndpoint = $"{bffOptions.Authority.ToString().TrimEnd('/')}/connect/revoke";

            // Revoke refresh token first (cascades to access token in OpenIddict)
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                await RevokeTokenAsync(
                    httpClient, revokeEndpoint, tokens.RefreshToken, "refresh_token",
                    frontend, tokens, clock, dpopService, cancellationToken).ConfigureAwait(false);
            }

            // Explicitly revoke access token as well (defense in depth)
            await RevokeTokenAsync(
                httpClient, revokeEndpoint, tokens.AccessToken, "access_token",
                frontend, tokens, clock, dpopService, cancellationToken).ConfigureAwait(false);

            LogTokensRevoked(logger, frontend.Name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort — logout must not fail because of revocation errors
            LogRevocationFailed(logger, ex, frontend.Name);
        }
    }

    private static async Task RevokeTokenAsync(
        HttpClient httpClient,
        string revokeEndpoint,
        string token,
        string tokenTypeHint,
        BffFrontendOptions frontend,
        BffTokenSet tokens,
        IClock clock,
        IDPoPProofService dpopService,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new()
        {
            ["token"] = token,
            ["token_type_hint"] = tokenTypeHint,
            ["client_id"] = frontend.ClientId,
        };

        ResolveClientAuth(frontend, clock).Apply(parameters, frontend.ClientId, revokeEndpoint);

        using FormUrlEncodedContent content = new(parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, revokeEndpoint) { Content = content };

        // Attach DPoP proof if active
        if (!string.IsNullOrEmpty(tokens.DPoPPrivateKeyJwk))
        {
            string dpopProof = dpopService.CreateProof(tokens.DPoPPrivateKeyJwk, "POST", revokeEndpoint, tokens.DPoPNonce);
            request.Headers.TryAddWithoutValidation("DPoP", dpopProof);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        // RFC 7009: server returns 200 on success, any other status is logged but not fatal
    }
#pragma warning restore GRSEC003

    private static IClientAuthenticationStrategy ResolveClientAuth(BffFrontendOptions frontend, IClock clock) =>
        frontend.ClientAuthenticationMethod == BffClientAuthenticationMethod.PrivateKeyJwt
            ? new PrivateKeyJwtStrategy(frontend.ClientSigningKeyJwk!, clock)
            : new ClientSecretPostStrategy(frontend.ClientSecret);

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF logout: session {SessionId} cleared for frontend {FrontendName}")]
    private static partial void LogLogout(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF logout: tokens revoked at authorization server for frontend {FrontendName}")]
    private static partial void LogTokensRevoked(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout: token revocation failed for frontend {FrontendName} (best-effort, logout continues)")]
    private static partial void LogRevocationFailed(ILogger logger, Exception exception, string frontendName);
}
