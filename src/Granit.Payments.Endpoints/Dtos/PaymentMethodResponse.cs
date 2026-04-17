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
/// <param name="MethodType">Stable method identifier.</param>
/// <param name="Category">Grouping category for the checkout UI.</param>
/// <param name="ProviderName">Provider that will process charges for this method.</param>
/// <param name="DisplayLabel">Localized display label.</param>
/// <param name="Capability">
/// Capability snapshot for this method (countries, currencies, bounds, sequence types).
/// <see langword="null"/> when the method was activated before snapshotting was introduced —
/// callers should treat null as wildcard.
/// </param>
public sealed record PaymentAvailableMethodResponse(
    string MethodType,
    PaymentMethodCategory Category,
    string ProviderName,
    string DisplayLabel,
    PaymentMethodCapabilityResponse? Capability);
