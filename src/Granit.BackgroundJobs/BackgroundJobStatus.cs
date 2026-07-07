namespace Granit.BackgroundJobs;

/// <summary>
/// Immutable snapshot of the current state of a recurring job.
/// Returned by <see cref="IBackgroundJobReader.GetAllAsync"/> and
/// <see cref="IBackgroundJobReader.FindAsync"/>.
/// </summary>
/// <param name="JobName">Unique job identifier.</param>
/// <param name="CronExpression">Cron expression defining the schedule.</param>
/// <param name="IsEnabled">Whether the job is active (<c>false</c> = paused).</param>
/// <param name="LastExecutedAt">UTC timestamp of the last execution start. Null if never run.</param>
/// <param name="NextExecutionAt">UTC timestamp of the next scheduled execution.</param>
/// <param name="ConsecutiveFailures">Number of consecutive failures since the last success.</param>
/// <param name="DeadLetterCount">
/// Number of messages in the Wolverine Dead Letter Queue for this job type.
/// Returns <c>0</c> when no durable messaging (<c>Granit.BackgroundJobs.Wolverine</c>) is available.
/// </param>
/// <param name="LastError">Error message from the last failure. Null on success.</param>
public sealed record BackgroundJobStatus(
    string JobName,
    string CronExpression,
    bool IsEnabled,
    DateTimeOffset? LastExecutedAt,
    DateTimeOffset? NextExecutionAt,
    int ConsecutiveFailures,
    long DeadLetterCount,
    string? LastError);
