using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Admin write endpoints for terminating another user's sessions via the canonical
/// <see cref="IUserSessionManager"/>.
/// </summary>
internal static class IdentityProviderSessionWriteEndpoints
{
    internal static RouteGroupBuilder MapProviderSessionWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapDelete("/{sessionId}", TerminateSessionAsync)
            .WithName("TerminateIdentityProviderSession")
            .WithSummary("Terminates a specific user session.")
            .WithDescription("Terminates the specified session, forcing the user to re-authenticate on that device. Returns 501 if the provider does not support individual session termination, 404 when no such session exists.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapDelete("/", TerminateAllSessionsAsync)
            .WithName("TerminateAllIdentityProviderSessions")
            .WithSummary("Terminates all active sessions for a user.")
            .WithDescription("Terminates every active session for the specified user, forcing re-authentication on all devices.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> TerminateSessionAsync(
        string userId,
        string sessionId,
        [FromServices] IUserSessionManager sessionManager,
        [FromServices] IIdentityProviderCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        if (!capabilities.SupportsIndividualSessionTermination)
        {
            return TypedResults.Problem(
                detail: $"The '{capabilities.ProviderName}' provider does not support individual session termination.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        bool revoked = await sessionManager.RevokeAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
        return revoked ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<NoContent> TerminateAllSessionsAsync(
        string userId,
        [FromServices] IUserSessionManager sessionManager,
        CancellationToken cancellationToken)
    {
        await sessionManager.RevokeAllAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
