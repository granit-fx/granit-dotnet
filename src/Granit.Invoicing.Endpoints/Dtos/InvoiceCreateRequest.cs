using Granit.Invoicing.Domain;

namespace Granit.Invoicing.Endpoints.Dtos;

/// <summary>Request to create a new invoice or credit note.</summary>
public sealed record InvoiceCreateRequest(
    Guid ContactId,
    InvoiceDocumentType DocumentType,
    string Currency,
    CollectionMethod CollectionMethod,
    BillingReason BillingReason,
    Guid? ParentInvoiceId = null,
    string? CreditNoteReason = null,
    DateTimeOffset? PeriodStart = null,
    DateTimeOffset? PeriodEnd = null);
