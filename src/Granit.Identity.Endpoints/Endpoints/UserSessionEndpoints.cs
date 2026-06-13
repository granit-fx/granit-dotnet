using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// The canonical <c>/sessions</c> endpoints: list, revoke one, revoke others — the caller's own sessions.
/// </summary>
internal static class UserSessionEndpoints
{
    /// <summary>Claim carrying the OIDC session id (<c>sid</c>), used to flag the current session.</summary>
    private const string SessionIdClaim = "sid";

    internal static RouteGroupBuilder MapUserSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListUserSessions")
            .WithSummary("Lists the caller's active sessions.")
            .WithDescription(
                "Returns the authenticated user's active sessions across the configured backend "
                + "(BFF, OpenIddict or Keycloak), each enriched with its approximate location and persisted "
                + "risk level. The current session is flagged. Raw IP addresses are never returned.")
            .Produces<IReadOnlyList<UserSessionResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{sessionId}", RevokeAsync)
            .WithName("RevokeUserSession")
            .WithSummary("Revokes one of the caller's sessions by ID.")
            .WithDescription("Revokes the specified session of the authenticated user. Returns 404 when no such session exists.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/", RevokeOthersAsync)
            .WithName("RevokeOtherUserSessions")
            .WithSummary("Revokes all of the caller's sessions except the current one.")
            .WithDescription("Revokes every session of the authenticated user except the one making the request, and returns how many were revoked.")
            .Produces<UserSessionsRevokedResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

#pragma warning disable GRAPI003 // Private handlers — resolved via the route delegates, not exposed as parameters
    private static async Task<Results<Ok<IReadOnlyList<UserSessionResponse>>, ProblemHttpResult>> ListAsync(
        [FromServices] IUserSessionManager manager,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IOptions<IdentityEndpointsOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return NoSubject();
        }

        IReadOnlyList<UserSessionView> views = await manager
            .ListAsync(currentUser.UserId, CurrentSessionId(httpContext), cancellationToken)
            .ConfigureAwait(false);

        bool exposeRawIp = options.Value.ExposeRawIpAddress;
        IReadOnlyList<UserSessionResponse> response = [.. views.Select(v => IdentityResponseMapper.ToResponse(v, exposeRawIp))];
        return TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> RevokeAsync(
        string sessionId,
        [FromServices] IUserSessionManager manager,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return NoSubject();
        }

        bool revoked = await manager.RevokeAsync(currentUser.UserId, sessionId, cancellationToken).ConfigureAwait(false);
        return revoked ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<UserSessionsRevokedResponse>, ProblemHttpResult>> RevokeOthersAsync(
        [FromServices] IUserSessionManager manager,
        [FromServices] ICurrentUserService currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return NoSubject();
        }

        string? currentSessionId = CurrentSessionId(httpContext);
        if (string.IsNullOrEmpty(currentSessionId))
        {
            return TypedResults.Problem(
                detail: "The current session could not be identified, so other sessions cannot be revoked safely.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        int revoked = await manager.RevokeOthersAsync(currentUser.UserId, currentSessionId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new UserSessionsRevokedResponse(revoked));
    }
#pragma warning restore GRAPI003

    private static string? CurrentSessionId(HttpContext httpContext) =>
        httpContext.User.FindFirst(SessionIdClaim)?.Value;

    private static ProblemHttpResult NoSubject() =>
        TypedResults.Problem(
            detail: "The request is not associated with a user subject.",
            statusCode: StatusCodes.Status401Unauthorized);
}
