using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Domain.ValueObjects;

namespace Granit.CustomerBalance;

/// <summary>Reads balance accounts (query side of CQRS).</summary>
public interface IBalanceAccountReader
{
    /// <summary>Returns a balance account by ID, including transactions.</summary>
    Task<BalanceAccount?> GetByIdAsync(BalanceAccountId id, CancellationToken cancellationToken = default);

    /// <summary>Returns the balance account for a tenant and currency.</summary>
    Task<BalanceAccount?> GetByTenantAndCurrencyAsync(Guid tenantId, string currency, CancellationToken cancellationToken = default);

    /// <summary>Returns all balance accounts for a tenant.</summary>
    Task<IReadOnlyList<BalanceAccount>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
