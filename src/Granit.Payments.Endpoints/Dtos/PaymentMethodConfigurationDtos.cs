using Granit.Payments.Domain;

namespace Granit.Payments.Endpoints.Dtos;

/// <summary>Response DTO for a payment method configuration.</summary>
public sealed record PaymentMethodConfigurationResponse(
    Guid Id,
    string MethodType,
    string ProviderName,
    string DisplayLabel,
    PaymentMethodCategory Category,
    bool IsActive);

/// <summary>Request to create a payment method configuration.</summary>
public sealed record CreatePaymentMethodConfigurationRequest(
    string MethodType,
    string ProviderName,
    string DisplayLabel);
