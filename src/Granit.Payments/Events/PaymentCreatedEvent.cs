using Granit.Events;

namespace Granit.Payments.Events;

/// <summary>Raised when a payment transaction is created (domain event).</summary>
public sealed record PaymentCreatedEvent(
    Guid TransactionId, Guid InvoiceId, Guid TenantId) : IDomainEvent;
