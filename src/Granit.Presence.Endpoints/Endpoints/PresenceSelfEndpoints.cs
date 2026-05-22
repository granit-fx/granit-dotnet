using System.Security.Claims;
using Granit.Presence.Abstractions;
using Granit.Presence.Domain;
using Granit.Presence.Endpoints.Dtos;
using Granit.Presence.Endpoints.Internal;
using Granit.Presence.Endpoints.Permissions;
using Granit.RateLimiting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Presence.Endpoints.Endpoints;

internal static class PresenceSelfEndpoints
{
    public static RouteGroupBuilder MapSelfEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/my", GetMyPresenceAsync)
            .WithName("GetMyPresence")
            .WithSummary("Returns the caller's current presence snapshot.")
            .WithDescription("Computes the effective presence status for the authenticated user by blending their manual override (if any) with their last reported heartbeat.")
            .Produces<PresenceResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/my", SetMyPresenceAsync)
            .WithName("SetMyPresence")
            .WithSummary("Sets or clears the caller's manual presence override.")
            .WithDescription("Replaces the caller's manual override. Passing ManualStatus=Available clears the override. UntilUtc is optional and bounded by Presence:MaxOverrideDuration.")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Mutate)
            .Produces<PresenceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/my/override", ClearMyPresenceAsync)
            .WithName("ClearMyPresenceOverride")
            .WithSummary("Clears the caller's manual presence override.")
            .WithDescription("Removes any active manual override so the effective status is derived from the heartbeat alone.")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Mutate)
            .Produces<PresenceResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/my/poll", PollMyPresenceAsync)
            .WithName("PollMyPresence")
            .WithSummary("Records a heartbeat and returns the caller's presence snapshot.")
            .WithDescription("Clients should call this every 30-60 seconds with the user's local idle duration (in seconds). The server reconstructs the LastActivityUtc using its own clock to avoid client clock-skew issues, and applies a MAX merge across concurrent tabs.")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Poll)
            .Produces<PresenceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return group;
    }

    private static async Task<Results<Ok<PresenceResponse>, ProblemHttpResult>> GetMyPresenceAsync(
        ClaimsPrincipal user,
        [FromServices] IPresenceQueryService queryService,
        CancellationToken cancellationToken)
    {
        if (!PresenceCallerContext.TryResolveSelf(user, out Guid userId, out ProblemHttpResult? unauthorized))
        {
            return unauthorized;
        }

        PresenceSnapshot snapshot = await queryService.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(PresenceResponseMapper.ToResponse(snapshot, isSelf: true));
    }

    private static async Task<Results<Ok<PresenceResponse>, ProblemHttpResult>> SetMyPresenceAsync(
        SetPresenceRequest request,
        ClaimsPrincipal user,
        [FromServices] IPresenceOverrideService overrideService,
        CancellationToken cancellationToken)
    {
        if (!PresenceCallerContext.TryResolveSelf(user, out Guid userId, out ProblemHttpResult? unauthorized))
        {
            return unauthorized;
        }

        PresenceSnapshot snapshot = await overrideService
            .SetAsync(userId, request.ManualStatus, request.UntilUtc, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(PresenceResponseMapper.ToResponse(snapshot, isSelf: true));
    }

    private static async Task<Results<Ok<PresenceResponse>, ProblemHttpResult>> ClearMyPresenceAsync(
        ClaimsPrincipal user,
        [FromServices] IPresenceOverrideService overrideService,
        CancellationToken cancellationToken)
    {
        if (!PresenceCallerContext.TryResolveSelf(user, out Guid userId, out ProblemHttpResult? unauthorized))
        {
            return unauthorized;
        }

        PresenceSnapshot snapshot = await overrideService
            .ClearAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(PresenceResponseMapper.ToResponse(snapshot, isSelf: true));
    }

    private static async Task<Results<Ok<PresenceResponse>, ProblemHttpResult>> PollMyPresenceAsync(
        HeartbeatRequest request,
        ClaimsPrincipal user,
        [FromServices] IPresenceHeartbeatRecorder recorder,
        CancellationToken cancellationToken)
    {
        if (!PresenceCallerContext.TryResolveSelf(user, out Guid userId, out ProblemHttpResult? unauthorized))
        {
            return unauthorized;
        }

        PresenceSnapshot snapshot = await recorder
            .RecordAsync(userId, TimeSpan.FromSeconds(request.IdleSeconds), cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(PresenceResponseMapper.ToResponse(snapshot, isSelf: true));
    }
}
