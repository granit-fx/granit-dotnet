using Granit.Invoicing.Domain;
using Granit.Invoicing.Dtos;

namespace Granit.Invoicing;

/// <summary>Invoice PDF generator (self-hosted via Templating or delegated to provider).</summary>
public interface IInvoiceDocumentGenerator
{
    /// <summary>Generates a PDF document for the invoice.</summary>
    Task<InvoiceDocumentResult> GenerateAsync(
        Invoice invoice, CancellationToken cancellationToken = default);
}
