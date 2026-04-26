using Granit.DocumentGeneration.Pipeline;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Dtos;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Builtin.Internal;

/// <summary>
/// Self-hosted invoice PDF generator using Granit.Templating + Granit.DocumentGeneration.
/// </summary>
internal sealed partial class BuiltinInvoiceDocumentGenerator(
    IDocumentGenerator documentGenerator,
    ILogger<BuiltinInvoiceDocumentGenerator> logger) : IInvoiceDocumentGenerator
{
    /// <inheritdoc/>
    public async Task<InvoiceDocumentResult> GenerateAsync(
        Invoice invoice, CancellationToken cancellationToken = default)
    {
        InvoiceTemplateData data = MapToTemplateData(invoice);

        DocumentResult result = await documentGenerator
            .GenerateAsync(InvoiceTemplateType.Instance, data, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string fileName = $"{invoice.InvoiceNumber ?? invoice.Id.ToString()}.pdf";

        Log.PdfGenerated(logger, invoice.Id, fileName);

        return new InvoiceDocumentResult(
            Content: result.Content.ToArray(),
            FileName: fileName,
            ContentType: "application/pdf");
    }

    private static InvoiceTemplateData MapToTemplateData(Invoice invoice) => new(
        InvoiceNumber: invoice.InvoiceNumber ?? string.Empty,
        DocumentType: invoice.IsCreditNote ? "Credit Note" : "Invoice",
        IssuedAt: invoice.IssuedAt ?? DateTimeOffset.MinValue,
        DueAt: invoice.DueAt,
        Currency: invoice.Currency,
        Subtotal: invoice.Subtotal,
        TaxTotal: invoice.TaxTotal,
        Total: invoice.Total,
        AmountPaid: invoice.AmountPaid,
        AmountRemaining: invoice.AmountRemaining,
        CreditNoteReason: invoice.CreditNoteReason,
        BillingAddress: invoice.IssuedBillingAddressSnapshot is not null
            ? new InvoiceAddressData(
                invoice.IssuedBillingAddressSnapshot.CompanyName,
                invoice.IssuedBillingAddressSnapshot.Line1,
                invoice.IssuedBillingAddressSnapshot.City,
                invoice.IssuedBillingAddressSnapshot.PostalCode,
                invoice.IssuedBillingAddressSnapshot.Country,
                invoice.IssuedBillingAddressSnapshot.VatNumber)
            : null,
        LineItems: invoice.LineItems.Select(li => new InvoiceLineItemData(
            li.Description,
            li.Quantity,
            li.UnitPrice,
            li.Quantity * li.UnitPrice,
            li.TaxRate,
            li.TaxAmount)).ToList());

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice PDF generated: {InvoiceId} -> {FileName}")]
        public static partial void PdfGenerated(ILogger logger, Guid invoiceId, string fileName);
    }
}
