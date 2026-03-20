using Granit.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Endpoints for managing user sessions via the identity provider.
/// </summary>
internal static class IdentityProviderSessionEndpoints
{
    internal static RouteGroupBuilder MapProviderSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserSessionsAsync)
            .WithName("GetIdentityProviderUserSessions")
            .WithSummary("Lists active sessions for a user.")
            .WithDescription("Returns all active sessions for the specified user from the identity provider.");

        group.MapDelete("/{sessionId}", TerminateSessionAsync)
            .WithName("TerminateIdentityProviderSession")
            .WithSummary("Terminates a specific user session.")
            .WithDescription("Terminates the specified session, forcing the user to re-authenticate on that device. Returns 501 if the provider does not support individual session termination.");

        group.MapDelete("/", TerminateAllSessionsAsync)
            .WithName("TerminateAllIdentityProviderSessions")
            .WithSummary("Terminates all active sessions for a user.")
            .WithDescription("Terminates every active session for the specified user, forcing re-authentication on all devices.");

        return group;
    }

    internal static RouteGroupBuilder MapProviderDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserDeviceActivityAsync)
            .WithName("GetIdentityProviderUserDevices")
            .WithSummary("Lists device activity for a user.")
            .WithDescription("Returns device activity information for the specified user, including device type, OS, browser, and associated sessions.");

        return group;
    }

    private static async Task<Ok<IReadOnlyList<IdentitySession>>> GetUserSessionsAsync(
        string userId,
        [FromServices] IIdentitySessionManager sessionManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentitySession> sessions = await sessionManager
            .GetUserSessionsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(sessions);
    }

    private static async Task<Ok<IReadOnlyList<IdentityDeviceActivity>>> GetUserDeviceActivityAsync(
        string userId,
        [FromServices] IIdentitySessionManager sessionManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityDeviceActivity> devices = await sessionManager
            .GetUserDeviceActivityAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(devices);
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
