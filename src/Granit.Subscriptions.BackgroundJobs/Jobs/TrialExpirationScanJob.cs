using Granit.BackgroundJobs;

namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Scans for trials expiring within 3 days (notification) and expired trials (transition).
/// </summary>
[RecurringJob("0 */4 * * *", "subscriptions-trial-expiration-scan")]
public sealed record TrialExpirationScanJob : IBackgroundJob;
