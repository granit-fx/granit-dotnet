namespace Granit.Notifications.Abstractions;

/// <summary>
/// Extension point consulted by the notification fanout pipeline after per-user
/// preferences are checked. A gate can veto delivery of a notification on a
/// specific channel for a specific user (for example to suppress push channels
/// while the user is in <c>DoNotDisturb</c> mode).
/// </summary>
/// <remarks>
/// <para>
/// Multiple gates can be registered. Delivery is allowed only when ALL gates return
/// <c>true</c> — i.e. AND semantics. Order is non-deterministic; gates MUST be
/// commutative.
/// </para>
/// <para>
/// Gates are skipped entirely for notifications whose
/// <see cref="NotificationDefinition.AllowDoNotDisturbBypass"/> is <c>true</c>
/// (security-critical alerts).
/// </para>
/// </remarks>
public interface INotificationDeliveryGate
{
    /// <summary>
    /// Returns <c>true</c> to allow delivery of the notification on the given channel
    /// for the given user, or <c>false</c> to suppress it. Implementations should be
    /// fast and tolerant — a thrown exception bubbles up to the fanout pipeline.
    /// </summary>
    Task<bool> ShouldDeliverAsync(
        string userId,
        string notificationTypeName,
        string channelName,
        Guid? tenantId,
        CancellationToken cancellationToken);
}
