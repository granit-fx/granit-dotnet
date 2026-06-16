namespace Granit.Notifications.Endpoints.Dtos;

/// <summary>
/// Response DTO for a notification delivery preference.
/// </summary>
public sealed record NotificationPreferenceResponse(
    Guid Id,
    string UserId,
    string NotificationTypeName,
    string ChannelName,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
