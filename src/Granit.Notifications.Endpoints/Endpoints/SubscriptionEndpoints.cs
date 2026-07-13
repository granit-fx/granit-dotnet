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
/// Minimal API endpoints for notification type subscriptions.
/// </summary>
internal static class SubscriptionEndpoints
{
    /// <summary>Maps all subscription endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapSubscriptionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions", GetSubscriptionsAsync)
            .RequireAuthorization(NotificationsPermissions.UserNotifications.Read)
            .WithName("GetSubscriptions")
            .WithSummary("Returns all notification subscriptions for the current user.")
            .WithDescription("Returns all notification type subscriptions for the authenticated user within the current tenant. Each subscription indicates a notification type the user has opted into.")
            .Produces<List<NotificationSubscriptionResponse>>();

        group.MapPost("/subscriptions/{typeName}", SubscribeAsync)
            .RequireAuthorization(NotificationsPermissions.UserNotifications.Manage)
            .WithName("Subscribe")
            .WithSummary("Subscribes the current user to a notification type.")
            .WithDescription("Subscribes the authenticated user to the specified notification type within the current tenant. Idempotent — subscribing to an already-subscribed type is a no-op.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/subscriptions/{typeName}", UnsubscribeAsync)
            .RequireAuthorization(NotificationsPermissions.UserNotifications.Manage)
            .WithName("Unsubscribe")
            .WithSummary("Unsubscribes the current user from a notification type.")
            .WithDescription("Removes the authenticated user's subscription to the specified notification type within the current tenant. Idempotent — unsubscribing from a non-subscribed type is a no-op.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Ok<List<NotificationSubscriptionResponse>>> GetSubscriptionsAsync(
        [FromServices] INotificationSubscriptionReader reader,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        IReadOnlyList<NotificationSubscription> subscriptions = await reader.GetUserSubscriptionsAsync(userId, tenantId).ConfigureAwait(false);
        return TypedResults.Ok(NotificationsResponseMapper.ToSubscriptionResponses(subscriptions));
    }

    private static async Task<NoContent> SubscribeAsync(
        string typeName,
        [FromServices] INotificationSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.SubscribeAsync(userId, typeName, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnsubscribeAsync(
        string typeName,
        [FromServices] INotificationSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.UnsubscribeAsync(userId, typeName, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
