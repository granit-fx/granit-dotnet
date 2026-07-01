using System.Text.Json;

namespace Granit.Notifications.WebPush;

/// <summary>Payload sent to the browser via Web Push.</summary>
public sealed record WebPushNotificationPayload
{
    /// <summary>Notification identifier.</summary>
    public Guid NotificationId { get; init; }

    /// <summary>Notification type name.</summary>
    public required string NotificationTypeName { get; init; }

    /// <summary>Notification severity.</summary>
    public required string Severity { get; init; }

    /// <summary>Notification data payload.</summary>
    public JsonElement Data { get; init; }

    /// <summary>When the notification occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}
