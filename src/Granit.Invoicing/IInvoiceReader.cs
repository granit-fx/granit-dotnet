using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;

namespace Granit.Invoicing;

/// <summary>Reads invoice data (query side of CQRS).</summary>
public interface IInvoiceReader
{
    Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetOverdueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Returns all credit notes linked to a parent invoice.</summary>
    Task<IReadOnlyList<Invoice>> GetCreditNotesForInvoiceAsync(InvoiceId parentInvoiceId, CancellationToken cancellationToken = default);
}
