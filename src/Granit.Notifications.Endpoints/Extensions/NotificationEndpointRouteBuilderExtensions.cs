using System.Security.Claims;
using Granit.Authorization;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Options;
using Granit.Notifications.Endpoints.Permissions;
using Granit.QueryEngine;
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
    public static IEndpointRouteBuilder MapGranitNotifications(
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
    }

    private static async Task<Ok<PagedResult<UserNotificationResponse>>> GetNotificationsAsync(
        [FromServices] IUserNotificationReader reader,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user,
        int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        PagedResult<UserNotification> result = await reader.GetListAsync(userId, tenantId, clampedPage, clampedPageSize).ConfigureAwait(false);
        return TypedResults.Ok(new PagedResult<UserNotificationResponse>(MapNotifications(result.Items), result.TotalCount, result.HasMore));
    }

    private static async Task<Ok<UnreadCountResponse>> GetUnreadCountAsync(
        [FromServices] IUserNotificationReader reader,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
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
        string userId = GetUserId(user);
        await writer.MarkAsReadAsync(id, userId, clock.Now).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> MarkAllAsReadAsync(
        [FromServices] IUserNotificationWriter writer,
        [FromServices] ICurrentTenant tenant,
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
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetEntityActivityFeed")
            .WithSummary("Returns the activity feed for a specific entity.")
            .WithDescription("Returns a paginated list of notifications related to a specific entity. Useful for displaying an activity log on an entity detail page. Results are sorted by creation date, newest first.")
            .Produces<PagedResult<UserNotificationResponse>>();
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
        return TypedResults.Ok(new PagedResult<UserNotificationResponse>(MapNotifications(result.Items), result.TotalCount, result.HasMore));
    }

    // -------------------------------------------------------------------------
    // Preferences
    // -------------------------------------------------------------------------

    private static void MapPreferenceEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/preferences", GetPreferencesAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetPreferences")
            .WithSummary("Returns notification delivery preferences for the current user.")
            .WithDescription("Returns all notification delivery preferences for the authenticated user within the current tenant. Each preference indicates whether a specific notification type is enabled or disabled for a given channel.")
            .Produces<List<NotificationPreferenceResponse>>();

        group.MapPut("/preferences", UpdatePreferenceAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("UpdatePreference")
            .WithSummary("Creates or updates a notification delivery preference.")
            .WithDescription("Creates or updates a delivery preference for a specific notification type and channel. If a preference already exists for the same type and channel, it is replaced (upsert).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        group.MapGet("/types", GetNotificationTypes)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetNotificationTypes")
            .WithSummary("Returns all registered notification type definitions.")
            .WithDescription("Returns all notification types registered in the system with their metadata. Use this to build the preferences UI, showing which notification types are available and their supported channels.")
            .Produces<IReadOnlyList<NotificationDefinition>>();
    }

    private static async Task<Ok<List<NotificationPreferenceResponse>>> GetPreferencesAsync(
        [FromServices] INotificationPreferenceReader reader,
        [FromServices] ICurrentTenant tenant,
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
        [FromServices] INotificationPreferenceWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] ICurrentTenant tenant,
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

    private static async Task<Ok<IReadOnlyList<NotificationDefinition>>> GetNotificationTypes(
        [FromServices] INotificationDefinitionStore definitionStore,
        [FromServices] IPermissionChecker permissionChecker,
        [FromServices] IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<NotificationDefinition> definitions = definitionStore.GetAll();
        if (definitions.Count == 0)
        {
            return TypedResults.Ok(definitions);
        }

        IReadOnlyList<NotificationDefinition> filtered = await FilterAsync(
            definitions, permissionChecker, serviceProvider, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(filtered);
    }

    private static async Task<IReadOnlyList<NotificationDefinition>> FilterAsync(
        IReadOnlyList<NotificationDefinition> definitions,
        IPermissionChecker permissionChecker,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        HashSet<string> grantedPermissions = await ResolveGrantedPermissionsAsync(
            definitions, permissionChecker, cancellationToken).ConfigureAwait(false);

        var featureGate = (INotificationFeatureGate?)serviceProvider.GetService(typeof(INotificationFeatureGate));
        HashSet<string> enabledFeatures = await ResolveEnabledFeaturesAsync(
            definitions, featureGate, cancellationToken).ConfigureAwait(false);

        List<NotificationDefinition> result = new(definitions.Count);
        foreach (NotificationDefinition def in definitions)
        {
            if (def.RequiredPermission is { Length: > 0 } perm
                && !grantedPermissions.Contains(perm))
            {
                continue;
            }

            if (def.RequiredFeature is { Length: > 0 } feat
                && featureGate is not null
                && !enabledFeatures.Contains(feat))
            {
                continue;
            }

            result.Add(def);
        }

        return result;
    }

    private static async Task<HashSet<string>> ResolveGrantedPermissionsAsync(
        IReadOnlyList<NotificationDefinition> definitions,
        IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
    {
        HashSet<string> requested = new(StringComparer.Ordinal);
        foreach (NotificationDefinition def in definitions)
        {
            if (def.RequiredPermission is { Length: > 0 } perm)
            {
                requested.Add(perm);
            }
        }

        if (requested.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            IReadOnlyList<string> granted = await permissionChecker.GetGrantedAsync(
                [.. requested], cancellationToken).ConfigureAwait(false);
            return new HashSet<string>(granted, StringComparer.Ordinal);
        }
        catch (InvalidOperationException)
        {
            // One of the referenced permissions wasn't declared in the host (typically because
            // the corresponding *.Endpoints module isn't mounted). Treat all of them as not
            // granted so the unfit notifications stay hidden from the preferences UI.
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static async Task<HashSet<string>> ResolveEnabledFeaturesAsync(
        IReadOnlyList<NotificationDefinition> definitions,
        INotificationFeatureGate? featureGate,
        CancellationToken cancellationToken)
    {
        HashSet<string> enabled = new(StringComparer.Ordinal);
        if (featureGate is null)
        {
            return enabled;
        }

        HashSet<string> requested = new(StringComparer.Ordinal);
        foreach (NotificationDefinition def in definitions)
        {
            if (def.RequiredFeature is { Length: > 0 } feat)
            {
                requested.Add(feat);
            }
        }

        foreach (string feature in requested)
        {
            try
            {
                if (await featureGate.IsFeatureEnabledAsync(feature, cancellationToken).ConfigureAwait(false))
                {
                    enabled.Add(feature);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Safe default: any other failure of a host-supplied gate keeps the notification hidden
                // rather than 500-ing the preferences endpoint. The gate impl is third-party from the
                // framework's perspective, so we can't narrow to a known exception type.
            }
        }

        return enabled;
    }

    // -------------------------------------------------------------------------
    // Subscriptions
    // -------------------------------------------------------------------------

    private static void MapSubscriptionEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions", GetSubscriptionsAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetSubscriptions")
            .WithSummary("Returns all notification subscriptions for the current user.")
            .WithDescription("Returns all notification type subscriptions for the authenticated user within the current tenant. Each subscription indicates a notification type the user has opted into.")
            .Produces<List<NotificationSubscriptionResponse>>();

        group.MapPost("/subscriptions/{typeName}", SubscribeAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("Subscribe")
            .WithSummary("Subscribes the current user to a notification type.")
            .WithDescription("Subscribes the authenticated user to the specified notification type within the current tenant. Idempotent — subscribing to an already-subscribed type is a no-op.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/subscriptions/{typeName}", UnsubscribeAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("Unsubscribe")
            .WithSummary("Unsubscribes the current user from a notification type.")
            .WithDescription("Removes the authenticated user's subscription to the specified notification type within the current tenant. Idempotent — unsubscribing from a non-subscribed type is a no-op.")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<Ok<List<NotificationSubscriptionResponse>>> GetSubscriptionsAsync(
        [FromServices] INotificationSubscriptionReader reader,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        IReadOnlyList<NotificationSubscription> subscriptions = await reader.GetUserSubscriptionsAsync(userId, tenantId).ConfigureAwait(false);
        return TypedResults.Ok(MapSubscriptions(subscriptions));
    }

    private static async Task<NoContent> SubscribeAsync(
        string typeName,
        [FromServices] INotificationSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = GetUserId(user);
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
    }

    private static async Task<NoContent> FollowEntityAsync(
        string entityType,
        string entityId,
        [FromServices] INotificationSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
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
        [FromServices] INotificationSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
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
        [FromServices] INotificationSubscriptionReader reader,
        [FromServices] ICurrentTenant tenant)
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
