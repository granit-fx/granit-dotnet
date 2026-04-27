using Granit.CustomerBalance.Domain;

namespace Granit.CustomerBalance;

/// <summary>Persists balance transaction changes (command side of CQRS).</summary>
/// <remarks>
/// Transactions are immutable except for the <c>LastExpirationNotifiedAt</c> dedupe
/// stamp written by the expiration scanner.
/// </remarks>
public interface IBalanceTransactionWriter
{
    /// <summary>
    /// Stamps <c>LastExpirationNotifiedAt</c> on the given promotional credit transaction
    /// so the daily expiration scanner does not re-emit a <c>CreditExpiringEto</c> for
    /// the same credit on its next run during the lead-time window.
    /// </summary>
    Task StampExpirationNotifiedAsync(
        Guid creditTransactionId,
        DateTimeOffset notifiedAt,
        CancellationToken cancellationToken = default);
}
