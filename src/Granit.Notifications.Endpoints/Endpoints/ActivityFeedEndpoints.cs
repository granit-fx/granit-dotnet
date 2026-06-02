using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Internal;
using Granit.Notifications.Endpoints.Permissions;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for the entity-scoped activity feed.
/// </summary>
internal static class ActivityFeedEndpoints
{
    /// <summary>Maps all activity feed endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapActivityFeedEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/entity/{entityType}/{entityId}", GetEntityActivityFeedAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetEntityActivityFeed")
            .WithSummary("Returns the activity feed for a specific entity.")
            .WithDescription("Returns a paginated list of notifications related to a specific entity. Useful for displaying an activity log on an entity detail page. Results are sorted by creation date, newest first.")
            .Produces<PagedResult<UserNotificationResponse>>();

        return group;
    }

    private static async Task<Ok<PagedResult<UserNotificationResponse>>> GetEntityActivityFeedAsync(
        string entityType,
        string entityId,
        [FromServices] IUserNotificationReader reader,
        [FromServices] ICurrentTenant tenant,
        int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        PagedResult<UserNotification> result = await reader.GetByEntityAsync(entityType, entityId, tenantId, clampedPage, clampedPageSize).ConfigureAwait(false);
        return TypedResults.Ok(new PagedResult<UserNotificationResponse>(NotificationsResponseMapper.ToNotificationResponses(result.Items), result.TotalCount, result.HasMore));
    }
}
