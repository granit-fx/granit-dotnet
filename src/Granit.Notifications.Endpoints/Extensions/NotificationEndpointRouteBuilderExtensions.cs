using System.Security.Claims;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Options;
using Granit.Querying;
using Granit.Timing;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit.Notifications REST endpoints.
/// </summary>
public static class NotificationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all Granit.Notifications REST endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="NotificationEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitNotificationEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<NotificationEndpointsOptions>? configure = null)
    {
        NotificationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        MapInboxEndpoints(group);
        MapActivityFeedEndpoints(group);
        MapPreferenceEndpoints(group);
        MapSubscriptionEndpoints(group);
        MapEntityFollowerEndpoints(group);

        return endpoints;
    }

    // -------------------------------------------------------------------------
    // Inbox
    // -------------------------------------------------------------------------

    private static void MapInboxEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/", GetNotificationsAsync)
            .WithName("GetNotifications")
            .WithSummary("Returns the user's notification inbox, newest first.");

        group.MapGet("/unread/count", GetUnreadCountAsync)
            .WithName("GetUnreadCount")
            .WithSummary("Returns the number of unread notifications for the current user.");

        group.MapPost("/{id:guid}/read", MarkAsReadAsync)
            .WithName("MarkAsRead")
            .WithSummary("Marks a single notification as read.");

        group.MapPost("/read-all", MarkAllAsReadAsync)
            .WithName("MarkAllAsRead")
            .WithSummary("Marks all notifications as read for the current user.");
    }

    private static async Task<Ok<PagedResult<UserNotificationResponse>>> GetNotificationsAsync(
        IUserNotificationReader reader,
        ICurrentTenant tenant,
        ClaimsPrincipal user,
        int page = 1, int pageSize = QueryingDefaults.DefaultPageSize)
    {
        int clampedPage = Math.Max(page, 1);
        int clampedPageSize = Math.Clamp(pageSize, 1, QueryingDefaults.MaxPageSize);

        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        PagedResult<UserNotification> result = await reader.GetListAsync(userId, tenantId, clampedPage, clampedPageSize).ConfigureAwait(false);
        return TypedResults.Ok(new PagedResult<UserNotificationResponse>(MapNotifications(result.Items), result.TotalCount, result.HasMore));
    }

    private static async Task<Ok<UnreadCountResponse>> GetUnreadCountAsync(
        IUserNotificationReader reader,
        ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        int count = await reader.GetUnreadCountAsync(userId, tenantId).ConfigureAwait(false);
        return TypedResults.Ok(new UnreadCountResponse(count));
    }

    private static async Task<NoContent> MarkAsReadAsync(
        Guid id,
        IUserNotificationWriter writer,
        [FromServices] IClock clock)
    {
        await writer.MarkAsReadAsync(id, clock.Now).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> MarkAllAsReadAsync(
        IUserNotificationWriter writer,
        ICurrentTenant tenant,
        ClaimsPrincipal user,
        [FromServices] IClock clock)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.MarkAllAsReadAsync(userId, tenantId, clock.Now).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // Activity feed
    // -------------------------------------------------------------------------

    private static void MapActivityFeedEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/entity/{entityType}/{entityId}", GetEntityActivityFeedAsync)
            .WithName("GetEntityActivityFeed")
            .WithSummary("Returns the activity feed for a specific entity.");
    }

    private static async Task<Ok<PagedResult<UserNotificationResponse>>> GetEntityActivityFeedAsync(
        string entityType,
        string entityId,
        IUserNotificationReader reader,
        ICurrentTenant tenant,
        int page = 1, int pageSize = QueryingDefaults.DefaultPageSize)
    {
        int clampedPage = Math.Max(page, 1);
        int clampedPageSize = Math.Clamp(pageSize, 1, QueryingDefaults.MaxPageSize);

        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        PagedResult<UserNotification> result = await reader.GetByEntityAsync(entityType, entityId, tenantId, clampedPage, clampedPageSize).ConfigureAwait(false);
        return TypedResults.Ok(new PagedResult<UserNotificationResponse>(MapNotifications(result.Items), result.TotalCount, result.HasMore));
    }

    // -------------------------------------------------------------------------
    // Preferences
    // -------------------------------------------------------------------------

    private static void MapPreferenceEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/preferences", GetPreferencesAsync)
            .WithName("GetPreferences")
            .WithSummary("Returns notification delivery preferences for the current user.");

        group.MapPut("/preferences", UpdatePreferenceAsync)
            .WithName("UpdatePreference")
            .WithSummary("Creates or updates a notification delivery preference.");

        group.MapGet("/types", GetNotificationTypes)
            .WithName("GetNotificationTypes")
            .WithSummary("Returns all registered notification type definitions.");
    }

    private static async Task<Ok<List<NotificationPreferenceResponse>>> GetPreferencesAsync(
        INotificationPreferenceReader reader,
        ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        IReadOnlyList<NotificationPreference> preferences = await reader.GetListAsync(userId, tenantId).ConfigureAwait(false);
        var result = preferences
            .Select(p => new NotificationPreferenceResponse(p.Id, p.UserId, p.NotificationTypeName, p.ChannelName, p.IsEnabled))
            .ToList();
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> UpdatePreferenceAsync(
        NotificationPreferenceUpdateRequest request,
        INotificationPreferenceWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        ICurrentTenant tenant,
        ClaimsPrincipal user,
        [FromServices] IClock clock)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        NotificationPreference preference = new()
        {
            Id = guidGenerator.Create(),
            UserId = userId,
            NotificationTypeName = request.NotificationTypeName,
            ChannelName = request.ChannelName,
            IsEnabled = request.IsEnabled,
            TenantId = tenantId,
            CreatedAt = clock.Now,
            CreatedBy = userId,
            ModifiedAt = clock.Now,
            ModifiedBy = userId,
        };
        await writer.SetAsync(preference).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static Ok<IReadOnlyList<NotificationDefinition>> GetNotificationTypes(
        INotificationDefinitionStore definitionStore)
    {
        IReadOnlyList<NotificationDefinition> definitions = definitionStore.GetAll();
        return TypedResults.Ok(definitions);
    }

    // -------------------------------------------------------------------------
    // Subscriptions
    // -------------------------------------------------------------------------

    private static void MapSubscriptionEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions", GetSubscriptionsAsync)
            .WithName("GetSubscriptions")
            .WithSummary("Returns all notification subscriptions for the current user.");

        group.MapPost("/subscriptions/{typeName}", SubscribeAsync)
            .WithName("Subscribe")
            .WithSummary("Subscribes the current user to a notification type.");

        group.MapDelete("/subscriptions/{typeName}", UnsubscribeAsync)
            .WithName("Unsubscribe")
            .WithSummary("Unsubscribes the current user from a notification type.");
    }

    private static async Task<Ok<List<NotificationSubscriptionResponse>>> GetSubscriptionsAsync(
        INotificationSubscriptionReader reader,
        ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        IReadOnlyList<NotificationSubscription> subscriptions = await reader.GetUserSubscriptionsAsync(userId, tenantId).ConfigureAwait(false);
        return TypedResults.Ok(MapSubscriptions(subscriptions));
    }

    private static async Task<NoContent> SubscribeAsync(
        string typeName,
        INotificationSubscriptionWriter writer,
        ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.SubscribeAsync(userId, typeName, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnsubscribeAsync(
        string typeName,
        INotificationSubscriptionWriter writer,
        ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.UnsubscribeAsync(userId, typeName, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // Entity followers
    // -------------------------------------------------------------------------

    private static void MapEntityFollowerEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/entity/{entityType}/{entityId}/follow", FollowEntityAsync)
            .WithName("FollowEntity")
            .WithSummary("Subscribes the current user as a follower of an entity.");

        group.MapDelete("/entity/{entityType}/{entityId}/follow", UnfollowEntityAsync)
            .WithName("UnfollowEntity")
            .WithSummary("Unsubscribes the current user from an entity.");

        group.MapGet("/entity/{entityType}/{entityId}/followers", GetEntityFollowersAsync)
            .WithName("GetEntityFollowers")
            .WithSummary("Returns all followers of a specific entity.");
    }

    private static async Task<NoContent> FollowEntityAsync(
        string entityType,
        string entityId,
        INotificationSubscriptionWriter writer,
        ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.FollowEntityAsync(userId, entityType, entityId, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnfollowEntityAsync(
        string entityType,
        string entityId,
        INotificationSubscriptionWriter writer,
        ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        await writer.UnfollowEntityAsync(userId, entityType, entityId, tenantId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<NotificationSubscriptionResponse>>> GetEntityFollowersAsync(
        string entityType,
        string entityId,
        INotificationSubscriptionReader reader,
        ICurrentTenant tenant)
    {
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        IReadOnlyList<NotificationSubscription> followers = await reader.GetEntityFollowersAsync(entityType, entityId, tenantId).ConfigureAwait(false);
        return TypedResults.Ok(MapSubscriptions(followers));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static List<UserNotificationResponse> MapNotifications(
        IReadOnlyList<UserNotification> notifications) =>
        notifications.Select(n => new UserNotificationResponse(
            n.Id, n.NotificationId, n.NotificationTypeName, n.Severity,
            n.RecipientUserId, n.Data.ValueKind == System.Text.Json.JsonValueKind.Undefined ? null : n.Data,
            n.State, n.CreatedAt, n.ReadAt, n.RelatedEntityType, n.RelatedEntityId)).ToList();

    private static List<NotificationSubscriptionResponse> MapSubscriptions(
        IReadOnlyList<NotificationSubscription> subscriptions) =>
        subscriptions.Select(s => new NotificationSubscriptionResponse(
            s.Id, s.UserId, s.NotificationTypeName, s.EntityType, s.EntityId)).ToList();

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identifier claim not found.");
}
