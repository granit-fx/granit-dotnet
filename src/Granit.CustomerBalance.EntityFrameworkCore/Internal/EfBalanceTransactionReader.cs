using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Domain.ValueObjects;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

internal sealed class EfBalanceTransactionReader(
    IDbContextFactory<CustomerBalanceDbContext> contextFactory)
    : EfStoreBase<BalanceTransaction, CustomerBalanceDbContext>(contextFactory),
      IBalanceTransactionReader, IBalanceTransactionWriter
{
    private readonly IDbContextFactory<CustomerBalanceDbContext> _contextFactory = contextFactory;

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

    public Task<IReadOnlyList<BalanceTransaction>> GetCreditsExpiringSoonAsync(
        DateTimeOffset now,
        DateTimeOffset leadTimeWindowEnd,
        DateTimeOffset cooldownThreshold,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<BalanceTransaction>()
                .Where(t =>
                    t.Source == TransactionSource.Promotional &&
                    t.Type == TransactionType.Credit &&
                    t.ExpiresAt != null &&
                    t.ExpiresAt > now &&
                    t.ExpiresAt <= leadTimeWindowEnd &&
                    (t.LastExpirationNotifiedAt == null || t.LastExpirationNotifiedAt < cooldownThreshold)),
            cancellationToken);

    public async Task StampExpirationNotifiedAsync(
        Guid creditTransactionId,
        DateTimeOffset notifiedAt,
        CancellationToken cancellationToken = default)
    {
        await using CustomerBalanceDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.Set<BalanceTransaction>()
            .Where(t => t.Id == creditTransactionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.LastExpirationNotifiedAt, notifiedAt),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
