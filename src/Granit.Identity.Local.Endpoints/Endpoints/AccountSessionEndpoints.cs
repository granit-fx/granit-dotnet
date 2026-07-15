using Granit.Http.SecurityHeaders.Extensions;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Extensions;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static class AccountSessionEndpoints
{
    internal static RouteGroupBuilder MapAccountSessionHeartbeatEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/session/heartbeat", (Delegate)HeartbeatAsync)
            .WithName("SessionHeartbeat")
            .WithSummary("Resets the idle session timer.")
            .WithDescription(
                "Updates LastActivityAt in the distributed cache. Returns 204 if the session "
                + "is still active. No-op for sessions with remember_me claim.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        return group;
    }

    internal static RouteGroupBuilder MapAccountBackToImpersonatorEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/session/back-to-impersonator", (Delegate)BackToImpersonatorAsync)
            .WithName("BackToImpersonator")
            .WithSummary("Ends an impersonation session and returns to the admin account.")
            .WithDescription(
                "Reads the impersonator_id claim from the current (impersonated) token "
                + "and issues a fresh token set for the original admin user. "
                + "Returns 400 if the current token is not an impersonation token.")
            .Produces<ImpersonationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization()
            .WithNoStoreResponse();

        return group;
    }

    private static async Task<NoContent> HeartbeatAsync(
        HttpContext httpContext,
        [FromServices] IUserSessionProvider sessionProvider,
        CancellationToken cancellationToken)
    {
        // remember_me sessions are exempt from idle enforcement, so recording their activity is
        // pointless. Every other backend touches last-access itself (BFF middleware, federated IdP)
        // and TouchAsync is a no-op there; only the OpenIddict authority persists activity here.
        if (httpContext.User.FindFirst("remember_me")?.Value is "true")
        {
            return TypedResults.NoContent();
        }

        await sessionProvider.TouchAsync(httpContext.User, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ImpersonationResponse>, ProblemHttpResult>> BackToImpersonatorAsync(
        HttpContext httpContext,
        [FromServices] IImpersonationService impersonationService,
        CancellationToken cancellationToken = default)
    {
        if (!httpContext.User.IsImpersonated())
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Impersonation:NotImpersonating", "Current session is not an impersonation session."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        string impersonatorId = httpContext.User.FindImpersonatorUserId()!;
        ImpersonationResult result = await impersonationService
            .BackToImpersonatorAsync(impersonatorId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(IdentityLocalResponseMapper.ToResponse(result));
    }
}
