using System.Text.Json;

namespace Granit.Notifications.Sse;

/// <summary>
/// DTO pushed to SSE clients when a notification is received.
/// </summary>
public sealed record SseNotificationMessage
{
    /// <summary>Unique identifier of the notification.</summary>
    public Guid NotificationId { get; init; }

    /// <summary>Notification type name from the definition registry.</summary>
    public string NotificationTypeName { get; init; } = string.Empty;

    /// <summary>Severity level of the notification.</summary>
    public NotificationSeverity Severity { get; init; }

    /// <summary>Notification payload as a JSON element.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>Related entity type, if any.</summary>
    public string? RelatedEntityType { get; init; }

    /// <summary>Related entity identifier, if any.</summary>
    public string? RelatedEntityId { get; init; }

    /// <summary>When the notification occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}
