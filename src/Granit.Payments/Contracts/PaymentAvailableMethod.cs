using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>Available payment method for checkout UI.</summary>
public sealed record PaymentAvailableMethod(
    string MethodType, PaymentMethodCategory Category,
    string ProviderName, string DisplayLabel);
