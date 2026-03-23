using System.Text.Json;
using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AccountSessionEndpoints
{
    internal static RouteGroupBuilder MapAccountSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/session/heartbeat", (Delegate)HeartbeatAsync)
            .WithName("SessionHeartbeat")
            .WithSummary("Resets the idle session timer.")
            .WithDescription(
                "Updates LastActivityAt in the distributed cache. Returns 204 if the session "
                + "is still active. No-op for sessions with remember_me claim.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        group.MapPost("/session/back-to-impersonator", (Delegate)BackToImpersonatorAsync)
            .WithName("BackToImpersonator")
            .WithSummary("Ends an impersonation session and returns to the admin account.")
            .WithDescription(
                "Reads the impersonator_id claim from the current (impersonated) token "
                + "and issues a fresh token set for the original admin user. "
                + "Returns 400 if the current token is not an impersonation token.")
            .Produces<ImpersonationResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        return group;
    }

    private static async Task<NoContent> HeartbeatAsync(
        HttpContext httpContext,
        [FromServices] IDistributedCache cache,
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

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(activity);
        await cache.SetAsync(cacheKey, serialized, new DistributedCacheEntryOptions
        {
            // Default: 35 min (30 min timeout + 5 min buffer).
            // Actual TTL adjusted by enforcement job based on per-tenant IdleSessionTimeout setting.
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(35),
        }, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ImpersonationResult>, ProblemHttpResult>> BackToImpersonatorAsync(
        HttpContext httpContext,
        [FromServices] IImpersonationService impersonationService,
        CancellationToken cancellationToken = default)
    {
        if (!httpContext.User.IsImpersonated())
        {
            return TypedResults.Problem(
                detail: "Current session is not an impersonation session.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string impersonatorId = httpContext.User.FindImpersonatorUserId()!;
        ImpersonationResult result = await impersonationService
            .BackToImpersonatorAsync(impersonatorId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }
}
