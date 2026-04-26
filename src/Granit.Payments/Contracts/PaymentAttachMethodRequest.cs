namespace Granit.Payments.Contracts;

/// <summary>Request to attach a payment method.</summary>
public sealed record PaymentAttachMethodRequest(
    Guid ContactId, string Type, string Token);
