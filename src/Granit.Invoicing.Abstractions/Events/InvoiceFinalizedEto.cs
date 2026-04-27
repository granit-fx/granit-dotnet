using Granit.Events;
using Granit.Invoicing.Domain;

namespace Granit.Invoicing.Events;

/// <summary>Published when an invoice is finalized. Triggers payment collection.</summary>
public sealed record InvoiceFinalizedEto(
    Guid InvoiceId, Guid TenantId, Guid PartyId, decimal Total,
    string Currency, CollectionMethod CollectionMethod) : IIntegrationEvent;
