using Granit.Domain;

namespace Granit.Notifications.Domain;

/// <summary>
/// ISO 27001 audit record for a notification delivery attempt: one immutable row per
/// <see cref="DeliveryId"/> keyed by channel fan-out semantics.
/// </summary>
/// <remarks>
/// <para>
/// Rows are finalized through <see cref="INotificationDeliveryWriter.CompleteDeliveryAttemptAsync"/>:
/// <see cref="IsSuccess"/> is <see langword="null"/> between claim and terminal outcome (successful or failed delivery).
/// This lets the dispatcher claim the row before invoking SMTP so concurrent workers cannot duplicate sends.
/// </para>
/// <para>
/// After retention expires (HDS minimization policy), callers may purge rows via batch delete.
/// </para>
/// </remarks>
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

    /// <summary><see langword="null"/> while the outbound send is still in-flight.</summary>
    public bool? IsSuccess { get; set; }
}
