namespace Granit.CustomerBalance;

/// <summary>
/// Scans expired promotional credits and debits remaining balances.
/// Publishes <see cref="Events.CreditExpiredEto"/> for each expired credit.
/// </summary>
public interface ICreditExpirationService
{
    /// <summary>
    /// Scans all expired promotional credits and processes them (debit + event).
    /// </summary>
    /// <returns>The number of credits that were expired.</returns>
    Task<int> ExpireCreditsAsync(CancellationToken cancellationToken = default);
}
