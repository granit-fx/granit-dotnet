using Granit.Payments.Domain;
using Granit.Payments.Domain.ValueObjects;

namespace Granit.Payments;

/// <summary>Reads payment transaction data (query side of CQRS).</summary>
public interface IPaymentTransactionReader
{
    Task<PaymentTransaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetByProviderTransactionIdAsync(string providerName, string providerTransactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentTransaction>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentTransaction>> GetForTenantAsync(CancellationToken cancellationToken = default);
}
