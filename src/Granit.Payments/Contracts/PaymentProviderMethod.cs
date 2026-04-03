using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>Payment method from provider.</summary>
public sealed record PaymentProviderMethod(
    string ProviderMethodId, PaymentMethodType Type,
    string DisplayLabel, DateTimeOffset? ExpiresAt);
