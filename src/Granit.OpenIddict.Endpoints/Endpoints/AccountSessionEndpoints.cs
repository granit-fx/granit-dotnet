using Granit.OpenIddict.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AccountSessionEndpoints
{
    internal static RouteGroupBuilder MapAccountSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/session/heartbeat", (Delegate)HeartbeatAsync)
            .WithName("SessionHeartbeat")
            .WithSummary("Resets the idle session timer.")
            .WithDescription(
                "Updates LastActivityAt in the session cache. Returns 204 if the session "
                + "is still active. The idle session enforcement job uses this timestamp "
                + "to determine whether to revoke refresh tokens.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        group.MapPost("/session/back-to-impersonator", (Delegate)BackToImpersonatorAsync)
            .WithName("BackToImpersonator")
            .WithSummary("Ends an impersonation session and returns to the admin account.")
            .WithDescription(
                "Reads the impersonator_id claim from the current (impersonated) token "
                + "and issues a fresh token set for the original admin user. "
                + "Returns 400 if the current token is not an impersonation token.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        return group;
    }

    private static Task<NoContent> HeartbeatAsync(HttpContext httpContext)
    {
        // TODO: Update ICacheService<UserSessionActivity> with key session:{userId}:{jti}
        // Cache TTL = IdleSessionTimeout + 5 min
        return Task.FromResult(TypedResults.NoContent());
    }

    private static Task<Results<Ok, ProblemHttpResult>> BackToImpersonatorAsync(
        HttpContext httpContext)
    {
        if (!httpContext.User.IsImpersonated())
        {
            return Task.FromResult<Results<Ok, ProblemHttpResult>>(
                TypedResults.Problem(
                    detail: "Current session is not an impersonation session.",
                    statusCode: StatusCodes.Status400BadRequest));
        }

        // TODO: Read impersonator_id, issue fresh admin tokens, revoke impersonation refresh token
        return Task.FromResult<Results<Ok, ProblemHttpResult>>(TypedResults.Ok());
    }
}
