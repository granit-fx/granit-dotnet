using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Internal;
using Granit.Notifications.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for entity-follower subscriptions.
/// </summary>
internal static class EntityFollowerEndpoints
{
    /// <summary>Maps all entity follower endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapEntityFollowerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/entity/{entityType}/{entityId}/follow", FollowEntityAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("FollowEntity")
            .WithSummary("Subscribes the current user as a follower of an entity.")
            .WithDescription("Adds the authenticated user as a follower of the specified entity within the current tenant. Followers receive notifications when activity occurs on the entity. Idempotent.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/entity/{entityType}/{entityId}/follow", UnfollowEntityAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("UnfollowEntity")
            .WithSummary("Unsubscribes the current user from an entity.")
            .WithDescription("Removes the authenticated user from the follower list of the specified entity within the current tenant. The user will no longer receive entity-level notifications. Idempotent.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/entity/{entityType}/{entityId}/followers", GetEntityFollowersAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetEntityFollowers")
            .WithSummary("Returns all followers of a specific entity.")
            .WithDescription("Returns all users following the specified entity within the current tenant. Each entry includes the user ID and subscription metadata.")
            .Produces<List<NotificationSubscriptionResponse>>();

        return group;
    }

    private static async Task<NoContent> FollowEntityAsync(
        string entityType,
        string entityId,
        [FromServices] INotificationSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.FollowEntityAsync(userId, entityType, entityId, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnfollowEntityAsync(
        string entityType,
        string entityId,
        [FromServices] INotificationSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.UnfollowEntityAsync(userId, entityType, entityId, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<NotificationSubscriptionResponse>>> GetEntityFollowersAsync(
        string entityType,
        string entityId,
        [FromServices] INotificationSubscriptionReader reader,
        [FromServices] ICurrentTenant tenant)
    {
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        IReadOnlyList<NotificationSubscription> followers = await reader.GetEntityFollowersAsync(entityType, entityId, tenantId).ConfigureAwait(false);
        return TypedResults.Ok(NotificationsResponseMapper.ToSubscriptionResponses(followers));
    }
}
