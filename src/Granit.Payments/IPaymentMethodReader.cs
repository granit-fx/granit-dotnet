using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>Reads saved payment method data.</summary>
public interface IPaymentMethodReader
{
    /// <summary>Returns a payment method by ID.</summary>
    Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all saved payment methods for a tenant.</summary>
    Task<IReadOnlyList<PaymentMethod>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns the default payment method for a tenant.</summary>
    Task<PaymentMethod?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
