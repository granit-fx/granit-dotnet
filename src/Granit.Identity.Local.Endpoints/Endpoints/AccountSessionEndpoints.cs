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
using ZiggyCreatures.Caching.Fusion;

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
        [FromServices] IFusionCache cache,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        string? userId = httpContext.User.FindFirst("sub")?.Value;
        string? jti = httpContext.User.FindFirst("jti")?.Value;

        // Skip if remember_me or missing claims
        if (userId is null || jti is null
            || httpContext.User.FindFirst("remember_me")?.Value is "true")
        {
            return TypedResults.NoContent();
        }

        string cacheKey = $"session:{userId}:{jti}";
        UserSessionActivity activity = new(userId, jti, timeProvider.GetUtcNow());

        await cache.SetAsync(
            cacheKey,
            activity,
            new FusionCacheEntryOptions
            {
                // Default: 35 min (30 min timeout + 5 min buffer).
                // Actual TTL adjusted by enforcement job based on per-tenant IdleSessionTimeout setting.
                Duration = TimeSpan.FromMinutes(35),
            },
            token: cancellationToken).ConfigureAwait(false);

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
