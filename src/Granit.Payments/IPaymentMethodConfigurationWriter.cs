using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>Persists payment method configuration changes.</summary>
public interface IPaymentMethodConfigurationWriter
{
    /// <summary>Adds a new configuration.</summary>
    Task AddAsync(PaymentMethodConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing configuration.</summary>
    Task UpdateAsync(PaymentMethodConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>Deletes a configuration.</summary>
    Task DeleteAsync(PaymentMethodConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the activation state for a (providerName, methodType) pair in a race-safe way.
    /// If no record exists, inserts one with the given id. If two concurrent calls race on
    /// insert, the losing call silently re-reads and updates instead (no exception surfaced).
    /// </summary>
    Task UpsertActivationAsync(
        Guid newId,
        string providerName,
        string methodType,
        bool isActive,
        CancellationToken cancellationToken = default);
}
