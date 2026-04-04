using Granit.BackgroundJobs;

namespace Granit.CustomerBalance.BackgroundJobs.Jobs;

/// <summary>
/// Scans for expired promotional credits and debits the remaining balance.
/// Runs every 6 hours.
/// </summary>
[RecurringJob("0 */6 * * *", "customer-balance-credit-expiration-scan")]
public sealed record CreditExpirationScanJob : IBackgroundJob;
