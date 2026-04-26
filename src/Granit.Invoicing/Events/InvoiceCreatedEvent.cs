using Granit.Events;

namespace Granit.Invoicing.Events;

/// <summary>Raised when a draft invoice is created.</summary>
public sealed record InvoiceCreatedEvent(Guid InvoiceId, Guid TenantId, Guid ContactId) : IDomainEvent;
