using Granit.Notifications;

namespace Granit.Scheduling.Notifications;

/// <summary>
/// Notification type fired when a scheduled action (one-shot or recurring instance) has
/// failed after exhausting retries — sent to tenant administrators so business-critical
/// scheduled work (reports, batch exports, periodic syncs) cannot fail silently for days.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> because the failure
/// is terminal for that occurrence — operators must investigate to reschedule or fix the
/// underlying payload. Recipients are resolved through the standard subscription mechanism:
/// admins opt in via the notifications admin UI rather than being hardcoded into options.
/// </remarks>
public sealed class SchedulingActionFailedNotificationType
    : NotificationType<SchedulingActionFailedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly SchedulingActionFailedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "scheduling.action_failed";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a scheduled-action failure notification.
/// </summary>
/// <param name="ActionId">Unique identifier of the scheduled action.</param>
/// <param name="PayloadType">CLR type name of the failed payload (the "what" — e.g.
/// <c>MonthlyComplianceReportPayload</c>).</param>
/// <param name="CorrelationId">Optional correlation identifier linking the action back
/// to a domain entity (invoice, subscription, ...). May be <see langword="null"/>.</param>
/// <param name="FailureReason">Truncated error message from the last attempt
/// (max 500 chars — see <c>ScheduledActionFailedEto.FailureReason</c>).</param>
/// <param name="FailedAt">UTC timestamp when the failure was recorded.</param>
public sealed record SchedulingActionFailedNotificationData(
    Guid ActionId,
    string PayloadType,
    string? CorrelationId,
    string FailureReason,
    DateTimeOffset FailedAt);
