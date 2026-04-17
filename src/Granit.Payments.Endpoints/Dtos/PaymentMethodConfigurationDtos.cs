using Granit.Payments.Domain;

namespace Granit.Payments.Endpoints.Dtos;

/// <summary>A single payment method declared by a provider, with its activation state.</summary>
public sealed record PaymentMethodConfigurationItem(
    string MethodType,
    string DisplayLabel,
    PaymentMethodCategory Category,
    bool IsActive);

/// <summary>All methods declared by a single provider, with their activation state.</summary>
public sealed record PaymentProviderConfigurationResponse(
    string ProviderName,
    IReadOnlyList<PaymentMethodConfigurationItem> Methods);
