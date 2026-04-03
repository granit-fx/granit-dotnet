using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>Request to attach a payment method.</summary>
public sealed record PaymentAttachMethodRequest(
    Guid TenantId, PaymentMethodType Type, string Token);
