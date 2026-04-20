using Granit.Invoicing.Domain;
using Granit.Timing;

namespace Granit.Invoicing.Internal;

/// <summary>
/// Default <see cref="IInvoiceCreditApplier"/> implementation — reads the invoice,
/// applies the credit note through the domain, and persists the change.
/// </summary>
internal sealed class DefaultInvoiceCreditApplier(
    IInvoiceReader invoiceReader,
    IInvoiceWriter invoiceWriter,
    IClock clock) : IInvoiceCreditApplier
{
    public async Task ApplyCreditAsync(
        Guid invoiceId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        Invoice? invoice = await invoiceReader
            .GetByIdAsync(invoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return;
        }

        invoice.ApplyCreditNote(amount, clock.Now);
        await invoiceWriter.UpdateAsync(invoice, cancellationToken).ConfigureAwait(false);
    }
}
