using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Domain.ValueObjects;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

internal sealed class EfBalanceTransactionReader(
    IDbContextFactory<CustomerBalanceDbContext> contextFactory)
    : EfStoreBase<BalanceTransaction, CustomerBalanceDbContext>(contextFactory),
      IBalanceTransactionReader
{
    private readonly IDbContextFactory<CustomerBalanceDbContext> _contextFactory = contextFactory;

    public async Task<IReadOnlyList<BalanceTransaction>> GetByAccountAsync(
        BalanceAccountId accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using CustomerBalanceDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Transactions
            .Where(t => t.BalanceAccountId == accountId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BalanceTransaction>> GetExpiredCreditsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using CustomerBalanceDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Transactions
            .Where(t =>
                t.Source == TransactionSource.Promotional &&
                t.Type == TransactionType.Credit &&
                t.ExpiresAt != null &&
                t.ExpiresAt <= now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
