using Granit.Domain;

namespace Granit.Notifications.Domain;

/// <summary>
/// INSERT-only ISO 27001 audit record for a notification delivery attempt.
/// Never modified or deleted.
/// </summary>
public sealed class NotificationDeliveryAttempt : Entity
{
    public Guid DeliveryId { get; set; }
    public Guid NotificationId { get; set; }
    public string NotificationTypeName { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess { get; set; }
}
