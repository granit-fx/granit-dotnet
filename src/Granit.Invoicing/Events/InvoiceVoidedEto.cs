using Granit.Events;

namespace Granit.Invoicing.Events;

/// <summary>Published when an invoice is voided.</summary>
public sealed record InvoiceVoidedEto(Guid InvoiceId, Guid TenantId) : IIntegrationEvent;
