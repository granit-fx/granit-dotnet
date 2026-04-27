using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Domain.ValueObjects;

namespace Granit.CustomerBalance;

/// <summary>Reads balance transactions (query side of CQRS).</summary>
public interface IBalanceTransactionReader
{
    /// <summary>Returns paginated transactions for a balance account.</summary>
    Task<IReadOnlyList<BalanceTransaction>> GetByAccountAsync(
        BalanceAccountId accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Returns promotional credit transactions that have expired but not yet been offset.</summary>
    Task<IReadOnlyList<BalanceTransaction>> GetExpiredCreditsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns promotional credit transactions whose <c>ExpiresAt</c> falls within the
    /// lead-time window <c>(now, now + leadTime]</c>, filtered by the per-credit
    /// notification cooldown (i.e. credits never alerted, or last alerted before
    /// <paramref name="cooldownThreshold"/>).
    /// </summary>
    /// <param name="now">Current timestamp; the lower bound of the lead-time window (exclusive).</param>
    /// <param name="leadTimeWindowEnd">Upper bound of the lead-time window (inclusive).</param>
    /// <param name="cooldownThreshold">
    /// Credits whose <c>LastExpirationNotifiedAt</c> is null OR strictly less than this
    /// timestamp are returned. Computed by the caller as
    /// <c>now - ExpirationNotificationCooldownDays</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<BalanceTransaction>> GetCreditsExpiringSoonAsync(
        DateTimeOffset now,
        DateTimeOffset leadTimeWindowEnd,
        DateTimeOffset cooldownThreshold,
        CancellationToken cancellationToken = default);
}
