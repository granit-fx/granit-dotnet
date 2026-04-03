using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>Persists payment transaction changes (command side of CQRS).</summary>
public interface IPaymentTransactionWriter
{
    Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
}
