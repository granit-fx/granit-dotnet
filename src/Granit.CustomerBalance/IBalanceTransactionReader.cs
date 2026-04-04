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
}
