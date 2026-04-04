using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Domain.ValueObjects;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

internal sealed class EfBalanceAccountStore(
    IDbContextFactory<CustomerBalanceDbContext> contextFactory)
    : EfStoreBase<BalanceAccount, CustomerBalanceDbContext>(contextFactory),
      IBalanceAccountReader, IBalanceAccountWriter
{
    private readonly IDbContextFactory<CustomerBalanceDbContext> _contextFactory = contextFactory;

    public Task<BalanceAccount?> GetByIdAsync(BalanceAccountId id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id.Value, cancellationToken);

    public async Task<BalanceAccount?> GetByTenantAndCurrencyAsync(
        Guid tenantId, string currency, CancellationToken cancellationToken = default)
    {
        string normalizedCurrency = currency.ToUpperInvariant();
        await using CustomerBalanceDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Accounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.Currency == normalizedCurrency,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<BalanceAccount>> GetByTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        ListAsync(Spec.For<BalanceAccount>().Where(a => a.TenantId == tenantId), cancellationToken);

    Task IBalanceAccountWriter.AddAsync(BalanceAccount account, CancellationToken cancellationToken) =>
        base.AddAsync(account, cancellationToken);

    Task IBalanceAccountWriter.UpdateAsync(BalanceAccount account, CancellationToken cancellationToken) =>
        base.UpdateAsync(account, cancellationToken);
}
