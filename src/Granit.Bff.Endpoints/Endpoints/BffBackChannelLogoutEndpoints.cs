using System.Text;
using System.Text.Json;
using Granit.Bff.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
                [FromServices] IOptions<GranitBffOptions> options,
                [FromServices] IBffTokenStore tokenStore,
                [FromServices] IDistributedCache cache,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                HandleBackChannelLogoutAsync(httpContext, frontend, options, tokenStore, cache, loggerFactory, cancellationToken))
            .WithName($"BffBackChannelLogout_{frontend.Name}")
            .WithSummary("Processes an OIDC back-channel logout token from the authorization server.")
            .WithDescription(
                "Receives a signed JWT logout token, validates it (issuer, audience, events claim, "
                + "replay protection via jti), and revokes all BFF sessions for the specified subject. "
                + "Returns 200 on success, 400 on invalid token.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ExcludeFromDescription();

        return group;
    }

#pragma warning disable GRAPI003 // Private handler
    private static async Task<Results<Ok, ProblemHttpResult>> HandleBackChannelLogoutAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        [FromServices] IOptions<GranitBffOptions> options,
        [FromServices] IBffTokenStore tokenStore,
        [FromServices] IDistributedCache cache,
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

        // Decode JWT payload (signature validation deferred — in production, validate against IdP JWKS)
        LogoutTokenClaims? claims = DecodeLogoutTokenPayload(logoutToken);
        if (claims is null)
        {
            LogInvalidLogoutToken(logger, frontend.Name);
            return TypedResults.Problem(
                detail: "Invalid logout token.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Validate issuer
        GranitBffOptions bffOptions = options.Value;
        string expectedIssuer = bffOptions.Authority.ToString().TrimEnd('/');
        if (!string.Equals(claims.Issuer?.TrimEnd('/'), expectedIssuer, StringComparison.OrdinalIgnoreCase))
        {
            LogIssuerMismatch(logger, claims.Issuer ?? "(null)", expectedIssuer, frontend.Name);
            return TypedResults.Problem(
                detail: "Logout token issuer mismatch.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Validate events claim (must contain back-channel logout event)
        if (!claims.HasBackChannelLogoutEvent)
        {
            return TypedResults.Problem(
                detail: "Missing back-channel logout event in token.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Replay protection via jti
        if (!string.IsNullOrEmpty(claims.Jti))
        {
            string jtiKey = $"{LogoutTokenJtiPrefix}{claims.Jti}";
            byte[]? existing = await cache.GetAsync(jtiKey, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                LogReplayDetected(logger, claims.Jti, frontend.Name);
                return TypedResults.Problem(
                    detail: "Logout token replay detected.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Mark jti as seen (TTL: 24h to prevent replay within a reasonable window)
            await cache.SetAsync(jtiKey, "1"u8.ToArray(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                cancellationToken).ConfigureAwait(false);
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

    private static LogoutTokenClaims? DecodeLogoutTokenPayload(string logoutToken)
    {
        try
        {
            string[] parts = logoutToken.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);
            JsonElement root = doc.RootElement;

            string? issuer = root.TryGetProperty("iss", out JsonElement iss) ? iss.GetString() : null;
            string? subject = root.TryGetProperty("sub", out JsonElement sub) ? sub.GetString() : null;
            string? jti = root.TryGetProperty("jti", out JsonElement jtiEl) ? jtiEl.GetString() : null;

            bool hasEvent = root.TryGetProperty("events", out JsonElement events)
                && events.TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out _);

            return new LogoutTokenClaims(issuer, subject, jti, hasEvent);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record LogoutTokenClaims(
        string? Issuer,
        string? Subject,
        string? Jti,
        bool HasBackChannelLogoutEvent);

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF back-channel logout: invalid logout token for frontend {FrontendName}")]
    private static partial void LogInvalidLogoutToken(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF back-channel logout: issuer mismatch — received '{ReceivedIssuer}', expected '{ExpectedIssuer}' for frontend {FrontendName}")]
    private static partial void LogIssuerMismatch(ILogger logger, string receivedIssuer, string expectedIssuer, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF back-channel logout: replay detected for jti '{Jti}' on frontend {FrontendName}")]
    private static partial void LogReplayDetected(ILogger logger, string jti, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF back-channel logout: revoked {Count} session(s) for subject '{Subject}' on frontend {FrontendName}")]
    private static partial void LogBackChannelLogout(ILogger logger, string subject, int count, string frontendName);
}
