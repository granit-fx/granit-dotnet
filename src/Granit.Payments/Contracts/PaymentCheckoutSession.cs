namespace Granit.Payments.Contracts;

/// <summary>Checkout session result (redirect URL).</summary>
public sealed record PaymentCheckoutSession(string Url, string SessionId, DateTimeOffset ExpiresAt);
