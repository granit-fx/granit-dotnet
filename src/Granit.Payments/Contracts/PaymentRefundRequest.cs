namespace Granit.Payments.Contracts;

/// <summary>Request to refund a payment.</summary>
public sealed record PaymentRefundRequest(
    string ProviderTransactionId, decimal Amount, string IdempotencyKey);
