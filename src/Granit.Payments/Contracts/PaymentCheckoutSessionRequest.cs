namespace Granit.Payments.Contracts;

/// <summary>Request to create a checkout session.</summary>
public sealed record PaymentCheckoutSessionRequest(
    Guid TransactionId, decimal Amount, string Currency,
    string MethodType, string SuccessUrl, string CancelUrl);
