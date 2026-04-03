using Granit.Events;

namespace Granit.Payments.Events;

/// <summary>Published when a refund completes. Consumed by Invoicing.</summary>
public sealed record RefundCompletedEto(
    Guid TransactionId, Guid RefundId, Guid InvoiceId, Guid TenantId,
    decimal Amount, string Currency) : IIntegrationEvent;
