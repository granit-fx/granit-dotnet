using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Granit.Http.RateLimiting.AspNetCore;
using Granit.Presence.Abstractions;
using Granit.Presence.Endpoints.Dtos;
using Granit.Presence.Endpoints.Internal;
using Granit.Presence.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Presence.Endpoints.Endpoints;

/// <summary>
/// Endpoints for resource-scoped multi-user awareness rooms — "who is also editing this CMS
/// page / dashboard / kanban card?".
/// </summary>
internal static class PresenceRoomEndpoints
{
    public static RouteGroupBuilder MapRoomEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/rooms/{kind}/{id}/heartbeat", HeartbeatRoomAsync)
            .WithName("HeartbeatPresenceRoom")
            .WithSummary("Joins or refreshes the caller's heartbeat in a resource room.")
            .WithDescription("Acts as both join and refresh — clients should call this every 30-60 s while the user remains in the room. Returns the full room snapshot to save a round-trip; participants are filtered through IResourcePresenceVisibilityPolicy.")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Poll)
            .RequireAuthorization(PresencePermissions.Rooms.Join)
            .Produces<ResourceRoomResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/rooms/{kind}/{id}", GetRoomAsync)
            .WithName("GetPresenceRoom")
            .WithSummary("Returns the participants currently active in a resource room.")
            .WithDescription("Filters stale entries (older than Presence:OfflineThreshold) and consults IResourcePresenceVisibilityPolicy. Policy denial surfaces as 404 to avoid leaking room existence — same anti-enumeration contract as user presence reads.")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Query)
            .RequireAuthorization(PresencePermissions.Rooms.Read)
            .Produces<ResourceRoomResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/rooms/{kind}/{id}", LeaveRoomAsync)
            .WithName("LeavePresenceRoom")
            .WithSummary("Removes the caller from a resource room.")
            .WithDescription("Idempotent — returns 204 whether or not the caller was present. The room cache entry is evicted entirely when the last participant leaves.")
            .RequireGranitRateLimiting(PresenceRateLimitPolicies.Mutate)
            .RequireAuthorization(PresencePermissions.Rooms.Join)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return group;
    }

    private static async Task<Results<Ok<ResourceRoomResponse>, ProblemHttpResult, NotFound, ValidationProblem>> HeartbeatRoomAsync(
        string kind,
        string id,
        HeartbeatRoomRequest? request,
        ClaimsPrincipal user,
        [FromServices] IResourcePresenceTracker tracker,
        [FromServices] IResourcePresenceVisibilityPolicy visibilityPolicy,
        CancellationToken cancellationToken)
    {
        if (!PresenceCallerContext.TryResolveSelf(user, out Guid userId, out ProblemHttpResult? unauthorized))
        {
            return unauthorized;
        }

        if (!TryValidateResource(kind, id, out ValidationProblem? badResource, out ResourceRef resource))
        {
            return badResource;
        }

        ResourceRoom room = await tracker
            .JoinAsync(resource, userId, request?.Metadata, cancellationToken)
            .ConfigureAwait(false);

        // Visibility policy still applies on the response — the caller may not be allowed
        // to read the room (would be 404 on a separate GET), so we mirror that here.
        bool canRead = await visibilityPolicy
            .CanReadRoomAsync(userId, resource, cancellationToken).ConfigureAwait(false);
        if (!canRead)
        {
            return TypedResults.NotFound();
        }

        IReadOnlySet<Guid> participantIds = room.Participants.Count == 0
            ? new HashSet<Guid>()
            : [.. room.Participants.Select(p => p.UserId)];

        IReadOnlySet<Guid> visible = await visibilityPolicy
            .FilterVisibleParticipantsAsync(userId, resource, participantIds, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(ResourceRoomResponseMapper.ToResponse(room, visible));
    }

    private static async Task<Results<Ok<ResourceRoomResponse>, ProblemHttpResult, NotFound, ValidationProblem>> GetRoomAsync(
        string kind,
        string id,
        ClaimsPrincipal user,
        [FromServices] IResourcePresenceTracker tracker,
        [FromServices] IResourcePresenceVisibilityPolicy visibilityPolicy,
        CancellationToken cancellationToken)
    {
        Guid callerId = PresenceCallerContext.TryGetUserId(user) ?? Guid.Empty;

        if (!TryValidateResource(kind, id, out ValidationProblem? badResource, out ResourceRef resource))
        {
            return badResource;
        }

        bool canRead = await visibilityPolicy
            .CanReadRoomAsync(callerId, resource, cancellationToken).ConfigureAwait(false);
        if (!canRead)
        {
            return TypedResults.NotFound();
        }

        ResourceRoom room = await tracker.GetAsync(resource, cancellationToken).ConfigureAwait(false);
        IReadOnlySet<Guid> participantIds = room.Participants.Count == 0
            ? new HashSet<Guid>()
            : [.. room.Participants.Select(p => p.UserId)];

        IReadOnlySet<Guid> visible = await visibilityPolicy
            .FilterVisibleParticipantsAsync(callerId, resource, participantIds, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(ResourceRoomResponseMapper.ToResponse(room, visible));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> LeaveRoomAsync(
        string kind,
        string id,
        ClaimsPrincipal user,
        [FromServices] IResourcePresenceTracker tracker,
        CancellationToken cancellationToken)
    {
        if (!PresenceCallerContext.TryResolveSelf(user, out Guid userId, out ProblemHttpResult? unauthorized))
        {
            return unauthorized;
        }

        if (!TryValidateResource(kind, id, out ValidationProblem? badResource, out ResourceRef resource))
        {
            return badResource;
        }

        await tracker.LeaveAsync(resource, userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Wraps <see cref="ResourceRef.Validate"/> so route-bound kind/id values surface as a
    /// 400 ProblemDetails (RFC 7807) rather than a 500.
    /// </summary>
    private static bool TryValidateResource(
        string kind,
        string id,
        [NotNullWhen(false)] out ValidationProblem? problem,
        out ResourceRef resource)
    {
        resource = new ResourceRef(kind, id);

        try
        {
            resource.Validate();
            problem = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            problem = TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "resource"] = [ex.Message],
            });
            return false;
        }
    }
}
