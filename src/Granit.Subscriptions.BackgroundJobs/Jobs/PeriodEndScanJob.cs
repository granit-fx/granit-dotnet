using Granit.BackgroundJobs;

namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Detects subscriptions reaching the end of their billing period and publishes
/// <c>BillingCycleCompletedEto</c> for the Billing module to generate invoices.
/// </summary>
[RecurringJob("0 * * * *", "subscriptions-period-end-scan")]
public sealed record PeriodEndScanJob : IBackgroundJob;
