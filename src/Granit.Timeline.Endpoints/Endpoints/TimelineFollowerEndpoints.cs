using Granit.Security;
using Granit.Timeline.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.Timeline.Endpoints.Endpoints;

/// <summary>
/// Endpoints for managing entity followers (follow, unfollow, list).
/// </summary>
internal static class TimelineFollowerEndpoints
{
    internal static RouteGroupBuilder MapFollowerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{entityType}/{entityId}/follow", FollowAsync)
            .WithName("FollowTimelineEntity")
            .WithSummary("Subscribes the current user as a follower of an entity.")
            .WithDescription("Adds the authenticated user to the follower list for the specified entity. Followers receive notifications when new timeline entries are posted. Idempotent — following an already-followed entity is a no-op.");

        group.MapDelete("/{entityType}/{entityId}/follow", UnfollowAsync)
            .WithName("UnfollowTimelineEntity")
            .WithSummary("Unsubscribes the current user from an entity.")
            .WithDescription("Removes the authenticated user from the follower list. The user will no longer receive notifications for new timeline entries on this entity. Idempotent.");

        group.MapGet("/{entityType}/{entityId}/followers", GetFollowersAsync)
            .WithName("GetTimelineFollowers")
            .WithSummary("Returns the user IDs of all followers of an entity.")
            .WithDescription("Returns the list of user IDs currently following the specified entity. Use the identity batch resolve endpoint to enrich these IDs with display names.");

        return group;
    }

    private static async Task<NoContent> FollowAsync(
        string entityType,
        string entityId,
        ITimelineFollowerService followerService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.UserId ?? string.Empty;
        await followerService.FollowAsync(userId, entityType, entityId, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnfollowAsync(
        string entityType,
        string entityId,
        ITimelineFollowerService followerService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.UserId ?? string.Empty;
        await followerService.UnfollowAsync(userId, entityType, entityId, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<string>>> GetFollowersAsync(
        string entityType,
        string entityId,
        ITimelineFollowerService followerService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> followers = await followerService.GetFollowerIdsAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(followers);
    }
}
