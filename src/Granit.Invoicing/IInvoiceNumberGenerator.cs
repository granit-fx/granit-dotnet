using Granit.Invoicing.Domain;

namespace Granit.Invoicing;

/// <summary>
/// Generates gap-free sequential document numbers for invoices and credit notes.
/// </summary>
/// <remarks>
/// Each <see cref="InvoiceDocumentType"/> uses a separate sequence
/// (e.g., INV-2026-0001 vs CN-2026-0001). Numbers are scoped per tenant and year.
/// Only used by self-hosted providers — external providers (Stripe, Odoo) supply
/// their own numbers.
/// </remarks>
public interface IInvoiceNumberGenerator
{
    /// <summary>Generates the next document number for the given type and tenant.</summary>
    /// <param name="documentType">Invoice or CreditNote — determines which sequence to use.</param>
    /// <param name="tenantId">Tenant scope for the sequence.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The formatted document number (e.g., "INV-2026-0042").</returns>
    Task<string> GenerateNextAsync(
        InvoiceDocumentType documentType,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
