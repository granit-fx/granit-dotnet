using Granit.BackgroundJobs;

namespace Granit.CustomerBalance.BackgroundJobs.Jobs;

/// <summary>
/// Scans promotional credits expiring within the configured warning window
/// (<c>CustomerBalanceOptions.PreExpirationWarningDays</c>, default 7) and
/// publishes <c>CreditNearExpirationEto</c> for each. Runs daily at 09:00 UTC.
/// </summary>
[RecurringJob("0 9 * * *", "customer-balance-credit-preexpiration-scan")]
public sealed record CreditPreExpirationScanJob : IBackgroundJob;
