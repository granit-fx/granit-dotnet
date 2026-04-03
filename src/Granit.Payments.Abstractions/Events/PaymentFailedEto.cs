using Granit.Events;

namespace Granit.Payments.Events;

/// <summary>Published when a payment fails. Consumed by Invoicing only.</summary>
public sealed record PaymentFailedEto(
    Guid TransactionId, Guid InvoiceId, Guid TenantId,
    string? FailureCode, string? FailureMessage) : IIntegrationEvent;
