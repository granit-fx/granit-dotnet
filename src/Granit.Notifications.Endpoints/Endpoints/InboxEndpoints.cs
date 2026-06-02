using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Internal;
using Granit.Notifications.Endpoints.Permissions;
using Granit.QueryEngine;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for the notification inbox (list, read, mark-read).
/// </summary>
internal static class InboxEndpoints
{
    /// <summary>Maps all inbox endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapInboxEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetNotificationsAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetNotifications")
            .WithSummary("Returns the user's notification inbox, newest first.")
            .WithDescription("Returns a paginated list of the current user's notifications. Supports filtering by read/unread status. Results are sorted by creation date, newest first.")
            .Produces<PagedResult<UserNotificationResponse>>();

        group.MapGet("/unread/count", GetUnreadCountAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetUnreadCount")
            .WithSummary("Returns the number of unread notifications for the current user.")
            .WithDescription("Returns the total count of unread notifications for the authenticated user within the current tenant. Use this to display a badge count in the UI.")
            .Produces<UnreadCountResponse>();

        group.MapPost("/{id:guid}/read", MarkAsReadAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("MarkAsRead")
            .WithSummary("Marks a single notification as read.")
            .WithDescription("Marks the specified notification as read by setting its read timestamp. Idempotent — marking an already-read notification is a no-op.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/read-all", MarkAllAsReadAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("MarkAllAsRead")
            .WithSummary("Marks all notifications as read for the current user.")
            .WithDescription("Marks all unread notifications as read for the authenticated user within the current tenant. Useful for a 'mark all as read' bulk action.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Ok<PagedResult<UserNotificationResponse>>> GetNotificationsAsync(
        [FromServices] IUserNotificationReader reader,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user,
        int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        PagedResult<UserNotification> result = await reader.GetListAsync(userId, tenantId, clampedPage, clampedPageSize).ConfigureAwait(false);
        return TypedResults.Ok(new PagedResult<UserNotificationResponse>(NotificationsResponseMapper.ToNotificationResponses(result.Items), result.TotalCount, result.HasMore));
    }

    private static async Task<Ok<UnreadCountResponse>> GetUnreadCountAsync(
        [FromServices] IUserNotificationReader reader,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        int count = await reader.GetUnreadCountAsync(userId, tenantId).ConfigureAwait(false);
        return TypedResults.Ok(new UnreadCountResponse(count));
    }

    private static async Task<NoContent> MarkAsReadAsync(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] IUserNotificationWriter writer,
        [FromServices] IClock clock)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        await writer.MarkAsReadAsync(id, userId, clock.Now).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> MarkAllAsReadAsync(
        [FromServices] IUserNotificationWriter writer,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user,
        [FromServices] IClock clock)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.MarkAllAsReadAsync(userId, tenantId, clock.Now).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
