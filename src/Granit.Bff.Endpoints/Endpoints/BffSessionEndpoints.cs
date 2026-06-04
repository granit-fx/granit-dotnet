using Granit.Bff.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF session management endpoints. Lists and revokes active sessions.
/// Registered per-frontend under <c>/{pathPrefix}/bff/sessions</c>.
/// </summary>
internal static partial class BffSessionEndpoints
{
    private const string NoActiveSessionMessage = "No active session.";
    internal static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapGet("/sessions", (HttpContext httpContext,
                [FromServices] IBffTokenStore tokenStore,
                [FromServices] IBffCsrfTokenGenerator csrfGenerator,
                CancellationToken cancellationToken) =>
                HandleListSessionsAsync(httpContext, frontend, tokenStore, cancellationToken))
            .WithName($"BffListSessions_{frontend.Name}")
            .WithSummary("Lists the current user's active sessions.")
            .WithDescription(
                "Returns a list of active BFF sessions for the authenticated user on this frontend. "
                + "Session IDs are masked for security. Requires a valid session cookie.")
            .Produces<BffSessionListResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ExcludeFromDescription();

        group.MapDelete("/sessions/{targetSessionId}", (HttpContext httpContext,
                string targetSessionId,
                [FromServices] IBffTokenStore tokenStore,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                HandleRevokeSessionAsync(httpContext, frontend, targetSessionId, tokenStore, loggerFactory, cancellationToken))
            .WithName($"BffRevokeSession_{frontend.Name}")
            .WithSummary("Revokes a specific session by ID.")
            .WithDescription(
                "Removes the specified session from the token store, effectively logging out that device. "
                + "Cannot revoke the current session (use /bff/logout instead).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ExcludeFromDescription();

        group.MapDelete("/sessions", (HttpContext httpContext,
                [FromServices] IBffTokenStore tokenStore,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                HandleRevokeAllOtherSessionsAsync(httpContext, frontend, tokenStore, loggerFactory, cancellationToken))
            .WithName($"BffRevokeAllSessions_{frontend.Name}")
            .WithSummary("Revokes all other sessions except the current one.")
            .WithDescription(
                "Removes all sessions for the current user on this frontend except the calling session. "
                + "Use case: 'Log out everywhere else'.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ExcludeFromDescription();

        return group;
    }

#pragma warning disable GRAPI003 // Private handler
    private static async Task<Results<Ok<BffSessionListResponse>, ProblemHttpResult>> HandleListSessionsAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        [FromServices] IBffTokenStore tokenStore,
        CancellationToken cancellationToken)
    {
        string? currentSessionId = httpContext.Request.Cookies[frontend.SessionCookieName];
        if (string.IsNullOrEmpty(currentSessionId))
        {
            return TypedResults.Problem(detail: NoActiveSessionMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

#pragma warning disable GRSEC003 // Reading tokens for session listing
        BffTokenSet? currentTokens = await tokenStore.GetAsync(frontend.Name, currentSessionId, cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore GRSEC003

        if (currentTokens?.UserId is null)
        {
            return TypedResults.Problem(detail: NoActiveSessionMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        IReadOnlyList<string> sessionIds = await tokenStore.GetSessionIdsByUserAsync(
            frontend.Name, currentTokens.UserId, cancellationToken).ConfigureAwait(false);

        List<BffSessionInfo> sessions = [];
        foreach (string sessionId in sessionIds)
        {
#pragma warning disable GRSEC003 // Reading tokens for session metadata
            BffTokenSet? tokens = await tokenStore.GetAsync(frontend.Name, sessionId, cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore GRSEC003

            if (tokens is not null)
            {
                sessions.Add(new BffSessionInfo(
                    SessionId: MaskSessionId(sessionId),
                    IsCurrent: sessionId == currentSessionId,
                    CreatedAt: tokens.SessionCreatedAt,
                    UserAgent: tokens.UserAgent));
            }
        }

        return TypedResults.Ok(new BffSessionListResponse(sessions));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleRevokeSessionAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        string targetSessionId,
        [FromServices] IBffTokenStore tokenStore,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Bff.Endpoints.BffSessionEndpoints");
        string? currentSessionId = httpContext.Request.Cookies[frontend.SessionCookieName];

        if (string.IsNullOrEmpty(currentSessionId))
        {
            return TypedResults.Problem(detail: NoActiveSessionMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

#pragma warning disable GRSEC003 // Reading tokens to resolve the calling user
        BffTokenSet? currentTokens = await tokenStore.GetAsync(frontend.Name, currentSessionId, cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore GRSEC003

        if (currentTokens?.UserId is null)
        {
            return TypedResults.Problem(detail: NoActiveSessionMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        // The /sessions list masks session IDs — raw IDs are never exposed to the
        // browser — so the caller can only return a masked ID. Resolve it back to
        // the raw session ID by recomputing the mask over the caller's OWN sessions.
        // Scoping the match to the current user means one user can never target
        // another's session, and the raw session ID stays server-side.
        IReadOnlyList<string> sessionIds = await tokenStore.GetSessionIdsByUserAsync(
            frontend.Name, currentTokens.UserId, cancellationToken).ConfigureAwait(false);

        var matches = sessionIds
            .Where(id => string.Equals(MaskSessionId(id), targetSessionId, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
        {
            return TypedResults.Problem(detail: "Session not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // A masked ID surfaces only 8 characters of a 43-character identifier; a
        // collision within a single user's sessions is astronomically unlikely,
        // but refuse to guess rather than revoke the wrong device.
        if (matches.Count > 1)
        {
            return TypedResults.Problem(
                detail: "Ambiguous session identifier.", statusCode: StatusCodes.Status409Conflict);
        }

        string rawSessionId = matches[0];
        await tokenStore.RemoveAsync(frontend.Name, rawSessionId, cancellationToken).ConfigureAwait(false);
        LogSessionRevoked(logger, MaskSessionId(rawSessionId), frontend.Name);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleRevokeAllOtherSessionsAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        [FromServices] IBffTokenStore tokenStore,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Bff.Endpoints.BffSessionEndpoints");
        string? currentSessionId = httpContext.Request.Cookies[frontend.SessionCookieName];

        if (string.IsNullOrEmpty(currentSessionId))
        {
            return TypedResults.Problem(detail: NoActiveSessionMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

#pragma warning disable GRSEC003
        BffTokenSet? currentTokens = await tokenStore.GetAsync(frontend.Name, currentSessionId, cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore GRSEC003

        if (currentTokens?.UserId is null)
        {
            return TypedResults.Problem(detail: NoActiveSessionMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        IReadOnlyList<string> sessionIds = await tokenStore.GetSessionIdsByUserAsync(
            frontend.Name, currentTokens.UserId, cancellationToken).ConfigureAwait(false);

        int revoked = 0;
        foreach (string sessionId in sessionIds.Where(id => id != currentSessionId))
        {
            await tokenStore.RemoveAsync(frontend.Name, sessionId, cancellationToken).ConfigureAwait(false);
            revoked++;
        }

        LogAllOtherSessionsRevoked(logger, revoked, frontend.Name);
        return TypedResults.NoContent();
    }
#pragma warning restore GRAPI003

    internal static string MaskSessionId(string sessionId) =>
        sessionId.Length > 8 ? $"{sessionId[..4]}...{sessionId[^4..]}" : "****";

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF session {SessionId} revoked for frontend {FrontendName}")]
    private static partial void LogSessionRevoked(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF revoked {Count} other session(s) for frontend {FrontendName}")]
    private static partial void LogAllOtherSessionsRevoked(ILogger logger, int count, string frontendName);
}

/// <summary>Response containing the user's active sessions.</summary>
internal sealed record BffSessionListResponse(List<BffSessionInfo> Sessions);

/// <summary>A single session entry.</summary>
internal sealed record BffSessionInfo(
    string SessionId,
    bool IsCurrent,
    DateTimeOffset CreatedAt,
    string? UserAgent);
