using System.Security.Claims;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;

namespace Granit.Notifications.Endpoints.Internal;

/// <summary>
/// Maps Granit.Notifications domain entities to response DTOs.
/// </summary>
internal static class NotificationsResponseMapper
{
    /// <summary>Maps a list of <see cref="UserNotification"/> to response records.</summary>
    public static List<UserNotificationResponse> ToNotificationResponses(
        IReadOnlyList<UserNotification> notifications) =>
        notifications.Select(n => new UserNotificationResponse(
            n.Id, n.NotificationId, n.NotificationTypeName, n.Severity,
            n.RecipientUserId, n.Data.ValueKind == System.Text.Json.JsonValueKind.Undefined ? null : n.Data,
            n.State, n.CreatedAt, n.ReadAt, n.RelatedEntityType, n.RelatedEntityId)).ToList();

    /// <summary>Maps a list of <see cref="NotificationSubscription"/> to response records.</summary>
    public static List<NotificationSubscriptionResponse> ToSubscriptionResponses(
        IReadOnlyList<NotificationSubscription> subscriptions) =>
        subscriptions.Select(s => new NotificationSubscriptionResponse(
            s.Id, s.UserId, s.NotificationTypeName, s.EntityType, s.EntityId)).ToList();

    /// <summary>Extracts the user identifier from the current <see cref="ClaimsPrincipal"/>.</summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when no user identifier claim is found.</exception>
    public static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identifier claim not found.");
}
