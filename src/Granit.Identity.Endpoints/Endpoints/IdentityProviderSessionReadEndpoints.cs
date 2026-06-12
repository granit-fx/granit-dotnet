using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Granit.Identity.Models;
using Granit.IpGeolocation;
using Granit.UserSessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Read endpoints for listing user sessions via the identity provider.
/// </summary>
internal static class IdentityProviderSessionReadEndpoints
{
    internal static RouteGroupBuilder MapProviderSessionReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserSessionsAsync)
            .WithName("GetIdentityProviderUserSessions")
            .WithSummary("Lists active sessions for a user.")
            .WithDescription("Returns all active sessions for the specified user from the identity provider.")
            .Produces<IReadOnlyList<IdentitySessionResponse>>();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<IdentitySessionResponse>>> GetUserSessionsAsync(
        string userId,
        [FromServices] IIdentitySessionManager sessionManager,
        [FromServices] IIpGeolocationResolver geoResolver,
        [FromServices] IUserSessionRiskStore riskStore,
        [FromServices] IOptions<IdentityEndpointsOptions> options,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentitySession> sessions = await sessionManager
            .GetUserSessionsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        List<IdentitySessionResponse> enriched = await IdentitySessionEnrichment
            .EnrichSessionsAsync(sessions, userId, geoResolver, riskStore, options.Value.ExposeRawIpAddress, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<IdentitySessionResponse>>(enriched);
    }
}
