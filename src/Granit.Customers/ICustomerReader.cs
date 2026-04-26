using Granit.Customers.Domain;
using Granit.Customers.Domain.ValueObjects;

namespace Granit.Customers;

/// <summary>Reader-side abstraction for the <see cref="Customer"/> aggregate (CQRS read).</summary>
/// <remarks>
/// All lookups honour the active tenant scope via the multi-tenant query filter — host-scoped
/// customers are visible only when no tenant context is active (or when the filter is bypassed
/// via <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c>).
/// </remarks>
public interface ICustomerReader
{
    /// <summary>Returns the customer with the given identifier, or <c>null</c>.</summary>
    Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the customer that holds an external mapping for <paramref name="providerName"/>
    /// equal to <paramref name="externalId"/>, or <c>null</c>.
    /// </summary>
    Task<Customer?> GetByExternalIdAsync(
        string providerName, string externalId, CancellationToken cancellationToken = default);

    /// <summary>Returns all customers in the active scope (tenant or host).</summary>
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken = default);
}
