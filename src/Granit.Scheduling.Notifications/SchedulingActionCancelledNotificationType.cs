using Granit.Notifications;

namespace Granit.Scheduling.Notifications;

/// <summary>
/// Notification type fired when a scheduled action is cancelled before execution —
/// surfaced in-app for administrators who opt in, so routine cancellations do not
/// compete with the higher-priority failure alert.
/// </summary>
/// <remarks>
/// Channels: InApp only (no Email — cancellation is lower priority than failure). Opt-in:
/// administrators subscribe explicitly rather than being enrolled by default. Recipients
/// are resolved through the standard subscription mechanism.
/// </remarks>
public sealed class SchedulingActionCancelledNotificationType
    : NotificationType<SchedulingActionCancelledNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly SchedulingActionCancelledNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "scheduling.action_cancelled";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } = [NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a scheduled-action cancellation notification.
/// </summary>
/// <param name="ActionId">Unique identifier of the scheduled action.</param>
/// <param name="CorrelationId">Optional correlation identifier linking the action back
/// to a domain entity (invoice, subscription, ...). May be <see langword="null"/>.</param>
/// <param name="CancelledBy">Optional identifier of the user who cancelled the action.
/// May be <see langword="null"/> when the cancellation was system-initiated.</param>
public sealed record SchedulingActionCancelledNotificationData(
    Guid ActionId,
    string? CorrelationId,
    string? CancelledBy);
