namespace Granit.Notifications.Endpoints.Dtos;

/// <summary>
/// Response DTO for a notification subscription or entity follower entry.
/// </summary>
public sealed record NotificationSubscriptionResponse(
    Guid Id,
    string UserId,
    string NotificationTypeName,
    string? EntityType,
    string? EntityId,
    DateTimeOffset CreatedAt);
