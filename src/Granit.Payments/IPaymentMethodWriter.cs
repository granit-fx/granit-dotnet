using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>Persists payment method changes.</summary>
public interface IPaymentMethodWriter
{
    Task AddAsync(PaymentMethod method, CancellationToken cancellationToken = default);
    Task UpdateAsync(PaymentMethod method, CancellationToken cancellationToken = default);
    Task DeleteAsync(PaymentMethod method, CancellationToken cancellationToken = default);
}
