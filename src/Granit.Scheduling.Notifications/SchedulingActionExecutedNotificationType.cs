using Granit.Notifications;

namespace Granit.Scheduling.Notifications;

/// <summary>
/// Notification type fired when a scheduled action (one-shot or recurring instance) has
/// executed successfully — surfaced in-app for administrators who opt in, so routine
/// completions do not compete with the higher-priority failure alert.
/// </summary>
/// <remarks>
/// Channels: InApp only (no Email — success is lower priority than failure). Opt-in:
/// administrators subscribe explicitly rather than being enrolled by default, since a
/// notification per successful run would otherwise spam anyone running frequent recurring
/// jobs. Recipients are resolved through the standard subscription mechanism.
/// </remarks>
public sealed class SchedulingActionExecutedNotificationType
    : NotificationType<SchedulingActionExecutedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly SchedulingActionExecutedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "scheduling.action_executed";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Success;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } = [NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a scheduled-action success notification.
/// </summary>
/// <param name="ActionId">Unique identifier of the scheduled action.</param>
/// <param name="PayloadType">CLR type name of the executed payload (the "what" — e.g.
/// <c>MonthlyComplianceReportPayload</c>).</param>
/// <param name="CorrelationId">Optional correlation identifier linking the action back
/// to a domain entity (invoice, subscription, ...). May be <see langword="null"/>.</param>
/// <param name="ExecutedAt">UTC timestamp when execution completed.</param>
public sealed record SchedulingActionExecutedNotificationData(
    Guid ActionId,
    string PayloadType,
    string? CorrelationId,
    DateTimeOffset ExecutedAt);
