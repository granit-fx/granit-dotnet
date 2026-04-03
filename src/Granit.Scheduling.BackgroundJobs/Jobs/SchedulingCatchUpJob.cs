using Granit.BackgroundJobs;

namespace Granit.Scheduling.BackgroundJobs.Jobs;

/// <summary>
/// Recurring safety-net job that detects overdue scheduled actions and re-dispatches them.
/// </summary>
/// <remarks>
/// <para>
/// Message brokers may lose scheduled messages over long periods (months).
/// This job scans for <c>ScheduledAction</c> entries with <c>Status == Pending</c>
/// and <c>ExecuteAt &lt; now - 5 min</c>, then re-dispatches their payloads
/// via Wolverine for immediate execution.
/// </para>
/// <para>
/// Cron overridable via <c>BackgroundJobs:Jobs:scheduling-catch-up</c>.
/// </para>
/// </remarks>
[RecurringJob("0 */6 * * *", "scheduling-catch-up")]
public sealed record SchedulingCatchUpJob : IBackgroundJob;
