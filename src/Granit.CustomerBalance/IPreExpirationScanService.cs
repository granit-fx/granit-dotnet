namespace Granit.CustomerBalance;

/// <summary>
/// Daily scan that publishes <see cref="Events.CreditNearExpirationEto"/> for every
/// promotional credit whose expiration falls within the
/// <c>CustomerBalanceOptions.PreExpirationWarningDays</c> window.
/// </summary>
/// <remarks>
/// Idempotent at the day-bucket level via
/// <see cref="Domain.BalanceTransaction.LastPreExpirationNoticedAt"/> — running
/// the scan twice in the same UTC day for the same credit publishes the event
/// only once. Already-expired credits are skipped (handled by
/// <see cref="ICreditExpirationService"/>).
/// </remarks>
public interface IPreExpirationScanService
{
    /// <summary>Runs the scan for the current instant. Returns the number of warnings published.</summary>
    Task<int> ScanAsync(CancellationToken cancellationToken = default);
}
