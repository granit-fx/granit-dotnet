using Granit.Contacts.Domain;

namespace Granit.Invoicing.Dtos;

/// <summary>Request for tax calculation.</summary>
public sealed record TaxRequest(
    IReadOnlyList<TaxLineItem> LineItems,
    BillingAddress SellerAddress,
    BillingAddress BuyerAddress);

/// <summary>A line item for tax calculation.</summary>
public sealed record TaxLineItem(string Description, decimal Amount, string? TaxCode);

/// <summary>Tax calculation result.</summary>
public sealed record TaxResult(
    IReadOnlyList<TaxLineResult> LineResults,
    decimal TotalTax,
    string? Jurisdiction);

/// <summary>Per-line tax result.</summary>
public sealed record TaxLineResult(decimal TaxAmount, decimal TaxRate, string? Jurisdiction);

/// <summary>Status from an external invoicing system.</summary>
public enum ExternalInvoiceStatus
{
    /// <summary>Invoice synced successfully.</summary>
    Synced = 0,

    /// <summary>Invoice marked as paid in external system.</summary>
    Paid = 1,

    /// <summary>Invoice voided in external system.</summary>
    Voided = 2,

    /// <summary>Sync error.</summary>
    Error = 3,
}

/// <summary>Result of invoice document generation.</summary>
public sealed record InvoiceDocumentResult(byte[] Content, string FileName, string ContentType);
