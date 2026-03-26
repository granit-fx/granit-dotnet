using Granit.Bff.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF back-channel logout endpoint (OIDC Back-Channel Logout 1.0).
/// Receives logout tokens from the authorization server and revokes matching sessions.
/// Registered per-frontend under <c>/{pathPrefix}/bff/backchannel-logout</c>.
/// </summary>
#pragma warning disable GRSEC003 // Cache key prefix, not a secret
internal static partial class BffBackChannelLogoutEndpoints
{
    private const string LogoutTokenJtiPrefix = "bff:bc-logout-jti:";
#pragma warning restore GRSEC003

    internal static RouteGroupBuilder MapBackChannelLogoutEndpoints(
        this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapPost("/backchannel-logout", (HttpContext httpContext,
                [FromServices] ILogoutTokenValidator tokenValidator,
                [FromServices] IBffTokenStore tokenStore,
                [FromServices] IFusionCache cache,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                HandleBackChannelLogoutAsync(httpContext, frontend, tokenValidator,
                    tokenStore, cache, loggerFactory, cancellationToken))
            .WithName($"BffBackChannelLogout_{frontend.Name}")
            .WithSummary("Processes an OIDC back-channel logout token from the authorization server.")
            .WithDescription(
                "Receives a signed JWT logout token, validates its signature against the IdP's "
                + "JWKS (auto-discovered), checks issuer, audience, and events claims, applies "
                + "replay protection via jti, and revokes all BFF sessions for the specified "
                + "subject. Returns 200 on success, 400 on invalid token.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ExcludeFromDescription();

        return group;
    }

#pragma warning disable GRAPI003 // Private handler
    private static async Task<Results<Ok, ProblemHttpResult>> HandleBackChannelLogoutAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        ILogoutTokenValidator tokenValidator,
        IBffTokenStore tokenStore,
        IFusionCache cache,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Bff.Endpoints.BffBackChannelLogoutEndpoints");

        // Read logout_token from form body (OIDC Back-Channel Logout spec sends as form POST)
        IFormCollection form = await httpContext.Request.ReadFormAsync(cancellationToken)
            .ConfigureAwait(false);
        string? logoutToken = form["logout_token"].ToString();

        if (string.IsNullOrEmpty(logoutToken))
        {
            return TypedResults.Problem(
                detail: "Missing logout_token parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Validate JWT signature, issuer, audience, and events claim
        ValidatedLogoutToken? claims = await tokenValidator.ValidateAsync(
            logoutToken, frontend.ClientId, cancellationToken).ConfigureAwait(false);

        if (claims is null)
        {
            LogInvalidLogoutToken(logger, frontend.Name);
            return TypedResults.Problem(
                detail: "Invalid logout token.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Replay protection via jti
        if (!string.IsNullOrEmpty(claims.Jti))
        {
            string jtiKey = $"{LogoutTokenJtiPrefix}{claims.Jti}";
            MaybeValue<bool> existing = await cache.TryGetAsync<bool>(jtiKey, token: cancellationToken)
                .ConfigureAwait(false);

            if (existing.HasValue)
            {
                LogReplayDetected(logger, claims.Jti, frontend.Name);
                return TypedResults.Problem(
                    detail: "Logout token replay detected.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Mark jti as seen (TTL: 24h to prevent replay within a reasonable window)
            await cache.SetAsync(
                jtiKey,
                true,
                new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(24) },
                token: cancellationToken).ConfigureAwait(false);
        }

        // Revoke sessions by subject
        if (!string.IsNullOrEmpty(claims.Subject))
        {
            IReadOnlyList<string> sessionIds = await tokenStore.GetSessionIdsByUserAsync(
                frontend.Name, claims.Subject, cancellationToken).ConfigureAwait(false);

            int revoked = 0;
            foreach (string sessionId in sessionIds)
            {
                await tokenStore.RemoveAsync(frontend.Name, sessionId, cancellationToken)
                    .ConfigureAwait(false);
                revoked++;
            }

            LogBackChannelLogout(logger, claims.Subject, revoked, frontend.Name);
        }

        return TypedResults.Ok();
    }
#pragma warning restore GRAPI003

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF back-channel logout: invalid logout token for frontend {FrontendName}")]
    private static partial void LogInvalidLogoutToken(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF back-channel logout: replay detected for jti '{Jti}' on frontend {FrontendName}")]
    private static partial void LogReplayDetected(ILogger logger, string jti, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF back-channel logout: revoked {Count} session(s) for subject '{Subject}' on frontend {FrontendName}")]
    private static partial void LogBackChannelLogout(ILogger logger, string subject, int count, string frontendName);
}
