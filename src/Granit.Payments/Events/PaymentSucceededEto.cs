using Granit.Events;

namespace Granit.Payments.Events;

/// <summary>Published when a payment succeeds. Consumed by Invoicing only.</summary>
public sealed record PaymentSucceededEto(
    Guid TransactionId, Guid InvoiceId, Guid TenantId,
    decimal Amount, string Currency, DateTimeOffset SucceededAt) : IIntegrationEvent;
