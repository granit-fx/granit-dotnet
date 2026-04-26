using Granit.Customers.Domain;

namespace Granit.Customers;

/// <summary>Writer-side abstraction for the <see cref="Customer"/> aggregate (CQRS write).</summary>
public interface ICustomerWriter
{
    /// <summary>Persists a new customer.</summary>
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing customer (state, contact, address, mappings).</summary>
    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}
