using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;

namespace Granit.Invoicing.Builtin.Internal;

/// <summary>
/// Document template type for invoice PDF generation.
/// Template name: <c>Invoicing.Invoice</c>.
/// </summary>
internal sealed class InvoiceTemplateType : DocumentTemplateType<InvoiceTemplateData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly InvoiceTemplateType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Invoicing.Invoice";

    /// <inheritdoc/>
    public override DocumentFormat DefaultFormat => DocumentFormat.Pdf;
}

/// <summary>
/// Data model for the invoice Scriban template.
/// </summary>
internal sealed record InvoiceTemplateData(
    string InvoiceNumber,
    string DocumentType,
    DateTimeOffset IssuedAt,
    DateTimeOffset? DueAt,
    string Currency,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    decimal AmountPaid,
    decimal AmountRemaining,
    string? CreditNoteReason,
    InvoiceAddressData? BillingAddress,
    IReadOnlyList<InvoiceLineItemData> LineItems);

/// <summary>Billing address for the template.</summary>
internal sealed record InvoiceAddressData(
    string? CompanyName,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country,
    string? VatNumber);

/// <summary>Line item for the template.</summary>
internal sealed record InvoiceLineItemData(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    decimal? TaxRate,
    decimal TaxAmount);
