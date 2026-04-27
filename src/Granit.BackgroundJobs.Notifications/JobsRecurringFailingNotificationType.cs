using Granit.Notifications;

namespace Granit.BackgroundJobs.Notifications;

/// <summary>
/// Notification type fired when a recurring job has failed the configured threshold of
/// consecutive runs (default: 3) — sent to platform / tenant administrators so SLA-critical
/// batches (compliance reports, billing rollups) cannot fail silently for hours.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> because the job is
/// not yet auto-paused — operators have time to investigate before automatic suspension.
/// Recipients are resolved through the standard subscription mechanism: admins opt in
/// via the notifications admin UI rather than being hardcoded into options.
/// </remarks>
public sealed class JobsRecurringFailingNotificationType
    : NotificationType<JobsRecurringFailingNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly JobsRecurringFailingNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "jobs.recurring_failing";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a recurring-job failure-threshold notification.
/// </summary>
/// <param name="JobId">Unique identifier of the job definition.</param>
/// <param name="JobName">Stable, human-readable job name (e.g. <c>blob-storage-orphan-cleanup</c>).</param>
/// <param name="ConsecutiveFailureCount">Number of consecutive failed runs that triggered the alert.</param>
/// <param name="LastErrorMessage">
/// Truncated error message from the most recent failure
/// (max 500 chars — see <c>BackgroundJobDefinition.MaxErrorMessageLength</c>).
/// May be <see langword="null"/> if the job did not surface an exception message.
/// </param>
public sealed record JobsRecurringFailingNotificationData(
    Guid JobId,
    string JobName,
    int ConsecutiveFailureCount,
    string? LastErrorMessage);
