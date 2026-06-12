using System.Diagnostics;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Bff.Options;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
        [FromServices] ILogoutTokenValidator tokenValidator,
        [FromServices] IBffTokenStore tokenStore,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory,
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
            await TryWriteAuditAsync(httpContext, logger, userId: null,
                failureReason: "invalid_logout_token", cancellationToken).ConfigureAwait(false);
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
                await TryWriteAuditAsync(httpContext, logger, userId: claims.Subject,
                    failureReason: "replay_detected", cancellationToken).ConfigureAwait(false);
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
            await TryWriteAuditAsync(httpContext, logger, userId: claims.Subject,
                failureReason: null, cancellationToken).ConfigureAwait(false);
        }

        return TypedResults.Ok();
    }

    /// <summary>
    /// Records a back-channel logout audit row. No-op when
    /// <see cref="IAuditingWriter"/> is not registered; audit failures are
    /// swallowed so the back-channel logout response is never blocked.
    /// </summary>
    private static async Task TryWriteAuditAsync(
        HttpContext httpContext,
        ILogger logger,
        string? userId,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        IAuditingWriter? auditingWriter = httpContext.RequestServices.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>()
            ?? TimeProvider.System;
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        string? correlationId = Activity.Current?.Id;
        const string Method = "bff_backchannel_logout";

        string successUserId = string.IsNullOrEmpty(userId)
            ? AuthenticationAuditEntry.UnknownUserSentinel
            : userId;
        AuditEntry entry = failureReason is null
            ? AuthenticationAuditEntry.CreateSuccess(
                timeProvider.GetUtcNow(),
                userId: successUserId,
                userName: null, method: Method,
                tenantId, ipAddress, userAgent, correlationId)
            : AuthenticationAuditEntry.CreateFailure(
                timeProvider.GetUtcNow(),
                userId, userName: null, method: Method, reason: failureReason,
                tenantId, ipAddress, userAgent, correlationId);

        try
        {
            await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, ex);
        }
    }
#pragma warning restore GRAPI003

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF back-channel logout: invalid logout token for frontend {FrontendName}")]
    private static partial void LogInvalidLogoutToken(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF back-channel logout: replay detected for jti '{Jti}' on frontend {FrontendName}")]
    private static partial void LogReplayDetected(ILogger logger, string jti, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF back-channel logout: revoked {Count} session(s) for subject '{Subject}' on frontend {FrontendName}")]
    private static partial void LogBackChannelLogout(ILogger logger, string subject, int count, string frontendName);

    [LoggerMessage(Level = LogLevel.Error, Message = "BFF back-channel logout: failed to write authentication audit entry — logout flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);
}
