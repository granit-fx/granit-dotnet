using Granit.Invoicing.Domain;

namespace Granit.Invoicing;

/// <summary>Persists invoice changes (command side of CQRS).</summary>
public interface IInvoiceWriter
{
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task DeleteAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
