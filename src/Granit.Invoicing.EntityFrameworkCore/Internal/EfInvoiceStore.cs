using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Parties.Domain.ValueObjects;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

internal sealed class EfInvoiceStore(
    IDbContextFactory<InvoicingDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<Invoice, InvoicingDbContext>(contextFactory, currentTenant),
      IInvoiceReader, IInvoiceWriter
{
    public Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id.Value, cancellationToken);

    public Task<Invoice?> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        ListAsync(Spec.For<Invoice>().Where(i => i.TenantId == tenantId), cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetByContactAsync(
        PartyId contactId, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<Invoice>().Where(i => i.PartyId.Value == contactId.Value),
            cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetOverdueAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<Invoice>().Where(i =>
                i.Status == InvoiceStatus.Open && i.DueAt != null && i.DueAt <= now),
            cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetCreditNotesForInvoiceAsync(
        InvoiceId parentInvoiceId, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<Invoice>().Where(i =>
                i.DocumentType == InvoiceDocumentType.CreditNote &&
                i.ParentInvoiceId != null &&
                i.ParentInvoiceId.Value == parentInvoiceId.Value),
            cancellationToken);

    Task IInvoiceWriter.AddAsync(Invoice invoice, CancellationToken cancellationToken) =>
        base.AddAsync(invoice, cancellationToken);

    Task IInvoiceWriter.UpdateAsync(Invoice invoice, CancellationToken cancellationToken) =>
        base.UpdateAsync(invoice, cancellationToken);

    Task IInvoiceWriter.DeleteAsync(Invoice invoice, CancellationToken cancellationToken) =>
        base.DeleteAsync(invoice, cancellationToken);
}
