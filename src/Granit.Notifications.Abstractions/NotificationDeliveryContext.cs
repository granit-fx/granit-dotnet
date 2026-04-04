using System.Text.Json;
using Granit.Domain;

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

    /// <summary>
    /// When set, bypasses <c>IRecipientResolver</c> and uses this contact info directly.
    /// Used for sending to addresses not yet in the identity store (email change, invitations).
    /// </summary>
    public RecipientInfo? RecipientOverride { get; init; }
}
