using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>Persistence for provider customer mappings (tenant → provider customer ID).</summary>
public interface IProviderCustomerMappingStore
{
    /// <summary>Returns the customer mapping for a tenant and provider.</summary>
    Task<ProviderCustomerMapping?> GetAsync(
        string providerName, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new customer mapping.</summary>
    Task AddAsync(ProviderCustomerMapping mapping, CancellationToken cancellationToken = default);
}
