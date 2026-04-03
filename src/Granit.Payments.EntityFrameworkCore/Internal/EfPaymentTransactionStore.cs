using Granit.Payments.Domain;
using Granit.Payments.Domain.ValueObjects;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class EfPaymentTransactionStore(
    IDbContextFactory<PaymentsDbContext> contextFactory)
    : EfStoreBase<PaymentTransaction, PaymentsDbContext>(contextFactory),
      IPaymentTransactionReader, IPaymentTransactionWriter
{
    public Task<PaymentTransaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id.Value, cancellationToken);

    public Task<PaymentTransaction?> GetByProviderTransactionIdAsync(
        string providerName, string providerTransactionId, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(
            t => t.ProviderName == providerName && t.ProviderTransactionId == providerTransactionId,
            cancellationToken);

    public Task<IReadOnlyList<PaymentTransaction>> GetForInvoiceAsync(
        Guid invoiceId, CancellationToken cancellationToken = default) =>
        ListAsync(Spec.For<PaymentTransaction>().Where(t => t.InvoiceId == invoiceId), cancellationToken);

    Task IPaymentTransactionWriter.AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken) =>
        base.AddAsync(transaction, cancellationToken);

    Task IPaymentTransactionWriter.UpdateAsync(PaymentTransaction transaction, CancellationToken cancellationToken) =>
        base.UpdateAsync(transaction, cancellationToken);
}
