namespace Granit.Payments.Contracts;

/// <summary>Payment method from provider.</summary>
public sealed record PaymentProviderMethod(
    string ProviderMethodId, string MethodType,
    string DisplayLabel, DateTimeOffset? ExpiresAt);
