using Granit.BackgroundJobs;

namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Processes subscriptions flagged with <c>CancelAtPeriodEnd = true</c> that have
/// passed their billing period end.
/// </summary>
[RecurringJob("0 */2 * * *", "subscriptions-cancel-at-period-end-scan")]
public sealed record CancelAtPeriodEndScanJob : IBackgroundJob;
