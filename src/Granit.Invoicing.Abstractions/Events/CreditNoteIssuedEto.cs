using Granit.Events;

namespace Granit.Invoicing.Events;

/// <summary>Published when a credit note is issued. Triggers refund via Payments.</summary>
public sealed record CreditNoteIssuedEto(
    Guid CreditNoteId, Guid InvoiceId, Guid TenantId, Guid PartyId, decimal Total) : IIntegrationEvent;
