using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Admin read endpoint for listing another user's sessions, served by the canonical
/// <see cref="IUserSessionManager"/> (geolocation + persisted risk applied once, no raw IP exposed).
/// </summary>
internal static class IdentityProviderSessionReadEndpoints
{
    internal static RouteGroupBuilder MapProviderSessionReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserSessionsAsync)
            .WithName("GetIdentityProviderUserSessions")
            .WithSummary("Lists active sessions for a user.")
            .WithDescription("Returns all active sessions for the specified user, enriched with approximate location and persisted risk level. Raw IP addresses are never returned.")
            .Produces<IReadOnlyList<UserSessionResponse>>();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<UserSessionResponse>>> GetUserSessionsAsync(
        string userId,
        [FromServices] IUserSessionManager sessionManager,
        [FromServices] IOptions<IdentityEndpointsOptions> options,
        CancellationToken cancellationToken)
    {
        // Admin view of another subject: no "current session" to flag.
        IReadOnlyList<UserSessionView> sessions = await sessionManager
            .ListAsync(userId, currentSessionId: null, cancellationToken)
            .ConfigureAwait(false);

        bool exposeRawIp = options.Value.ExposeRawIpAddress;
        IReadOnlyList<UserSessionResponse> response = [.. sessions.Select(v => IdentityResponseMapper.ToResponse(v, exposeRawIp))];
        return TypedResults.Ok(response);
    }
}
