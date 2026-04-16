using Granit.DataProtection;
using Granit.Events;

namespace Granit.Payments.Events;

/// <summary>Published when a payment fails. Consumed by dunning handler.</summary>
public sealed record PaymentFailedEto(
    Guid TransactionId, Guid InvoiceId, Guid TenantId,
    decimal Amount, string Currency,
    string ProviderName, string MethodType,
    string? FailureCode, [property: SensitiveData] string? FailureMessage) : IIntegrationEvent;
