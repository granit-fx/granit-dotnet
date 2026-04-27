namespace Granit.CustomerBalance;

/// <summary>
/// Proactive scanner that emits <c>CreditExpiringEto</c> for promotional credits whose
/// expiration falls within the configured lead-time window. Runs daily.
/// </summary>
/// <remarks>
/// Idempotent across runs: per-credit dedupe via
/// <c>BalanceTransaction.LastExpirationNotifiedAt</c> with a configurable cooldown
/// (<c>CustomerBalanceOptions.ExpirationNotificationCooldownDays</c>, default 7).
/// </remarks>
public interface ICreditExpiringScanService
{
    /// <summary>
    /// Scans for promotional credits expiring within the lead-time window and emits one
    /// <c>CreditExpiringEto</c> per match.
    /// </summary>
    /// <returns>Number of credits that triggered an Eto emission.</returns>
    Task<int> ScanAsync(CancellationToken cancellationToken = default);
}
