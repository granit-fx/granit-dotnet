using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>Reads payment method configurations (host-level activation records).</summary>
public interface IPaymentMethodConfigurationReader
{
    /// <summary>Returns all configurations (active and inactive).</summary>
    Task<IReadOnlyList<PaymentMethodConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns only active configurations.</summary>
    Task<IReadOnlyList<PaymentMethodConfiguration>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a configuration by its ID, or <c>null</c> if not found.</summary>
    Task<PaymentMethodConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds a configuration by provider name and method type.</summary>
    Task<PaymentMethodConfiguration?> FindAsync(string providerName, string methodType, CancellationToken cancellationToken = default);
}
