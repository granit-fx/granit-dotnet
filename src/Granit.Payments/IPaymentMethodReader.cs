using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>Reads saved payment method data.</summary>
public interface IPaymentMethodReader
{
    Task<IReadOnlyList<PaymentMethod>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PaymentMethod?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
