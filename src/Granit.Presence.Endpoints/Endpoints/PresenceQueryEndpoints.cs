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

internal static class PresenceQueryEndpoints
{
    public static RouteGroupBuilder MapQueryEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/users/{userId:guid}", GetUserPresenceAsync)
            .WithName("GetUserPresence")
            .WithSummary("Returns the presence snapshot of one user.")
            .WithDescription("Computes the effective presence status for the specified user. Always returns 200 OK — unknown, never-seen, and policy-denied targets are canonicalized to Offline with a null LastSeenUtc so the response shape cannot be used as an enumeration oracle. Visibility is decided by IPresenceVisibilityPolicy (default: allow-all; multi-tenant apps MUST register a tenant-aware policy).")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Query)
            .Produces<PresenceResponse>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/users/batch", GetBatchPresenceAsync)
            .WithName("GetBatchPresence")
            .WithSummary("Returns presence snapshots for a batch of users.")
            .WithDescription("Returns a dictionary keyed by user id. POST is used because UUID lists exceed practical URL length around 50 entries. Targets the caller is not allowed to read (per IPresenceVisibilityPolicy) are silently omitted from the response — not 403'd — to avoid leaking an enumeration channel.")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Query)
            .Produces<BatchPresenceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return group;
    }

    private static async Task<Ok<PresenceResponse>> GetUserPresenceAsync(
        Guid userId,
        ClaimsPrincipal user,
        [FromServices] IPresenceQueryService queryService,
        [FromServices] IPresenceVisibilityPolicy visibilityPolicy,
        [FromServices] IPresenceReadAuditSink auditSink,
        CancellationToken cancellationToken)
    {
        Guid callerId = PresenceCallerContext.TryGetUserId(user) ?? Guid.Empty;
        bool isSelf = callerId != Guid.Empty && callerId == userId;
        bool allowed = isSelf;

        if (!isSelf)
        {
            IReadOnlySet<Guid> visible = await visibilityPolicy
                .FilterVisibleAsync(callerId, [userId], cancellationToken).ConfigureAwait(false);
            allowed = visible.Contains(userId);
        }

        await auditSink.RecordReadAsync(
            callerId, targetCount: 1, allowedCount: allowed ? 1 : 0, isSelf, cancellationToken)
            .ConfigureAwait(false);

        if (!allowed)
        {
            return TypedResults.Ok(PresenceResponseMapper.Unknown(userId));
        }

        PresenceSnapshot snapshot = await queryService.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(PresenceResponseMapper.ToResponse(snapshot, isSelf));
    }

    private static async Task<Ok<BatchPresenceResponse>> GetBatchPresenceAsync(
        BatchPresenceRequest request,
        ClaimsPrincipal user,
        [FromServices] IPresenceQueryService queryService,
        [FromServices] IPresenceVisibilityPolicy visibilityPolicy,
        [FromServices] IPresenceReadAuditSink auditSink,
        CancellationToken cancellationToken)
    {
        Guid callerId = PresenceCallerContext.TryGetUserId(user) ?? Guid.Empty;

        List<Guid> others = new(request.UserIds.Count);
        bool selfRequested = false;
        foreach (Guid id in request.UserIds)
        {
            if (callerId != Guid.Empty && id == callerId)
            {
                selfRequested = true;
            }
            else
            {
                others.Add(id);
            }
        }

        IReadOnlySet<Guid> visibleOthers = others.Count == 0
            ? new HashSet<Guid>()
            : await visibilityPolicy.FilterVisibleAsync(callerId, others, cancellationToken).ConfigureAwait(false);

        List<Guid> allowedTargets = new(visibleOthers.Count + (selfRequested ? 1 : 0));
        if (selfRequested)
        {
            allowedTargets.Add(callerId);
        }
        allowedTargets.AddRange(visibleOthers);

        await auditSink.RecordReadAsync(
            callerId,
            targetCount: request.UserIds.Count,
            allowedCount: allowedTargets.Count,
            includesSelf: selfRequested,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<Guid, PresenceSnapshot> snapshots = allowedTargets.Count == 0
            ? new Dictionary<Guid, PresenceSnapshot>(0)
            : await queryService.GetManyAsync(allowedTargets, cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, PresenceResponse> responses = new(allowedTargets.Count);
        foreach (Guid id in allowedTargets)
        {
            bool isSelf = id == callerId && callerId != Guid.Empty;
            if (snapshots.TryGetValue(id, out PresenceSnapshot? snapshot))
            {
                responses[id] = PresenceResponseMapper.ToResponse(snapshot, isSelf);
            }
            else
            {
                responses[id] = PresenceResponseMapper.Unknown(id);
            }
        }

        return TypedResults.Ok(new BatchPresenceResponse(responses));
    }
}
