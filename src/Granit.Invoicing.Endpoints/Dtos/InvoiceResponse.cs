using Granit.Invoicing.Domain;

namespace Granit.Invoicing.Endpoints.Dtos;

/// <summary>Invoice or credit note details.</summary>
public sealed record InvoiceResponse(
    Guid Id,
    string DocumentType,
    string? InvoiceNumber,
    string Status,
    string CollectionMethod,
    string BillingReason,
    string Currency,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    decimal AmountPaid,
    decimal AmountCredited,
    decimal AmountRemaining,
    Guid? ParentInvoiceId,
    string? CreditNoteReason,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    IReadOnlyList<InvoiceLineItemResponse> LineItems)
{
    internal static InvoiceResponse FromEntity(Invoice invoice) => new(
        invoice.Id,
        invoice.DocumentType.ToString(),
        invoice.InvoiceNumber,
        invoice.Status.ToString(),
        invoice.CollectionMethod.ToString(),
        invoice.BillingReason.ToString(),
        invoice.Currency,
        invoice.Subtotal,
        invoice.TaxTotal,
        invoice.Total,
        invoice.AmountPaid,
        invoice.AmountCredited,
        invoice.AmountRemaining,
        invoice.ParentInvoiceId?.Value,
        invoice.CreditNoteReason,
        invoice.IssuedAt,
        invoice.DueAt,
        invoice.PaidAt,
        invoice.PeriodStart,
        invoice.PeriodEnd,
        invoice.LineItems.Select(InvoiceLineItemResponse.FromEntity).ToList());
}

/// <summary>Invoice line item details.</summary>
public sealed record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    decimal? TaxRate,
    decimal TaxAmount,
    string SourceType,
    string? SourceId,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd)
{
    internal static InvoiceLineItemResponse FromEntity(InvoiceLineItem lineItem) => new(
        lineItem.Id,
        lineItem.Description,
        lineItem.Quantity,
        lineItem.UnitPrice,
        lineItem.Amount,
        lineItem.TaxRate,
        lineItem.TaxAmount,
        lineItem.SourceType.ToString(),
        lineItem.SourceId,
        lineItem.PeriodStart,
        lineItem.PeriodEnd);
}
