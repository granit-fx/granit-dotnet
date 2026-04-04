using System.Text.Json;
using Granit.Domain;

namespace Granit.Notifications.Messages;

/// <summary>
/// Wolverine command for delivering a notification to one recipient via one channel.
/// One per user x channel, produced by <see cref="Handlers.NotificationFanoutHandler"/>.
/// </summary>
public sealed record DeliverNotificationCommand
{
    public required Guid DeliveryId { get; init; }
    public required Guid NotificationId { get; init; }
    public required string NotificationTypeName { get; init; }
    public required NotificationSeverity Severity { get; init; }
    public required string RecipientUserId { get; init; }
    public required string ChannelName { get; init; }
    public required JsonElement Data { get; init; }
    public EntityReference? RelatedEntity { get; init; }
    public Guid? TenantId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? Culture { get; init; }

    /// <summary>
    /// When set, bypasses <c>IRecipientResolver</c> for this delivery.
    /// </summary>
    public RecipientInfo? RecipientOverride { get; init; }
}
