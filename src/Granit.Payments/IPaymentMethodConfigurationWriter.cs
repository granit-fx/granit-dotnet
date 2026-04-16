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
}
