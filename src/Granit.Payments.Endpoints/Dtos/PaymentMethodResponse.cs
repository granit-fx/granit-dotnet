using Granit.Payments.Domain;

namespace Granit.Payments.Endpoints.Dtos;

/// <summary>
/// Read-only representation of a <see cref="PaymentMethod"/> for API responses.
/// </summary>
public sealed record PaymentMethodResponse(
    Guid Id,
    string Type,
    string ProviderName,
    string ProviderMethodId,
    string DisplayLabel,
    bool IsDefault,
    DateTimeOffset? ExpiresAt,
    Guid? TenantId);

/// <summary>
/// Available payment method that a tenant can use, as reported by the provider.
/// </summary>
public sealed record PaymentAvailableMethodResponse(
    string MethodType,
    PaymentMethodCategory Category,
    string ProviderName,
    string DisplayLabel);
