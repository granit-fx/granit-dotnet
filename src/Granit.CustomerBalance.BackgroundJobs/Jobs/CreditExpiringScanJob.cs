using Granit.BackgroundJobs;

namespace Granit.CustomerBalance.BackgroundJobs.Jobs;

/// <summary>
/// Scans for promotional credits whose expiration is approaching (within
/// <c>CustomerBalanceOptions.ExpirationLeadTimeDays</c>) and emits one
/// <c>CreditExpiringEto</c> per match. Runs daily at 07:00 UTC — chosen so the user
/// receives a "credit expiring soon" email at the start of their day, not in the middle
/// of the night.
/// </summary>
[RecurringJob("0 7 * * *", "customer-balance-credit-expiring-scan")]
public sealed record CreditExpiringScanJob : IBackgroundJob;
