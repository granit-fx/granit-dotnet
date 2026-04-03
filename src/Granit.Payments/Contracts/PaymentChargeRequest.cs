namespace Granit.Payments.Contracts;

/// <summary>Request to charge a customer.</summary>
public sealed record PaymentChargeRequest(
    Guid TransactionId, decimal Amount, string Currency,
    string IdempotencyKey, Guid? PaymentMethodId = null, string? ReturnUrl = null);
