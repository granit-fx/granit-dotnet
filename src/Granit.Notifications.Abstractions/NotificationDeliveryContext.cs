using System.Text.Json;

namespace Granit.Notifications;

/// <summary>
/// Context passed to <see cref="Abstractions.INotificationChannel.SendAsync"/> during delivery.
/// </summary>
public sealed record NotificationDeliveryContext
{
    public required Guid NotificationId { get; init; }
    public required Guid DeliveryId { get; init; }
    public required string NotificationTypeName { get; init; }
    public required NotificationSeverity Severity { get; init; }
    public required string RecipientUserId { get; init; }
    public required JsonElement Data { get; init; }
    public EntityReference? RelatedEntity { get; init; }
    public Guid? TenantId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string? Culture { get; init; }
}
