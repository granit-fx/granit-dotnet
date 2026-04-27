using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Domain.ValueObjects;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

internal sealed class EfBalanceTransactionReader(
    IDbContextFactory<CustomerBalanceDbContext> contextFactory)
    : EfStoreBase<BalanceTransaction, CustomerBalanceDbContext>(contextFactory),
      IBalanceTransactionReader
{
    public Task<IReadOnlyList<BalanceTransaction>> GetByAccountAsync(
        BalanceAccountId accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<BalanceTransaction>()
                .Where(t => t.BalanceAccountId == accountId.Value)
                .OrderByDescending(t => t.CreatedAt)
                .Paginate(page, pageSize),
            cancellationToken);

    public Task<IReadOnlyList<BalanceTransaction>> GetExpiredCreditsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<BalanceTransaction>()
                .Where(t =>
                    t.Source == TransactionSource.Promotional &&
                    t.Type == TransactionType.Credit &&
                    t.ExpiresAt != null &&
                    t.ExpiresAt <= now),
            cancellationToken);
}
