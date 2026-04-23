using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Write endpoints for terminating user sessions via the identity provider.
/// </summary>
internal static class IdentityProviderSessionWriteEndpoints
{
    internal static RouteGroupBuilder MapProviderSessionWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapDelete("/{sessionId}", TerminateSessionAsync)
            .WithName("TerminateIdentityProviderSession")
            .WithSummary("Terminates a specific user session.")
            .WithDescription("Terminates the specified session, forcing the user to re-authenticate on that device. Returns 501 if the provider does not support individual session termination.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapDelete("/", TerminateAllSessionsAsync)
            .WithName("TerminateAllIdentityProviderSessions")
            .WithSummary("Terminates all active sessions for a user.")
            .WithDescription("Terminates every active session for the specified user, forcing re-authentication on all devices.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> TerminateSessionAsync(
        string userId,
        string sessionId,
        [FromServices] IIdentitySessionManager sessionManager,
        [FromServices] IIdentityProviderCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        if (!capabilities.SupportsIndividualSessionTermination)
        {
            return TypedResults.Problem(
                detail: $"The '{capabilities.ProviderName}' provider does not support individual session termination.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        await sessionManager.TerminateSessionAsync(userId, sessionId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> TerminateAllSessionsAsync(
        string userId,
        [FromServices] IIdentitySessionManager sessionManager,
        CancellationToken cancellationToken)
    {
        await sessionManager.TerminateAllSessionsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
