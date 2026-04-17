using Granit.Payments.Domain;

namespace Granit.Payments.Endpoints.Dtos;

/// <summary>A single payment method declared by a provider, with its activation state.</summary>
/// <param name="MethodType">Stable method identifier (e.g., <c>card</c>, <c>bancontact</c>).</param>
/// <param name="DisplayLabel">Localized display label for the checkout UI.</param>
/// <param name="Category">Grouping category for the checkout UI.</param>
/// <param name="Activated">Whether the method is currently active on the platform.</param>
/// <param name="CapabilitySnapshot">Last-captured capability snapshot, or <see langword="null"/> when no snapshot has been taken yet.</param>
public sealed record PaymentMethodConfigurationItem(
    string MethodType,
    string DisplayLabel,
    PaymentMethodCategory Category,
    bool Activated,
    PaymentMethodCapabilityResponse? CapabilitySnapshot);

/// <summary>All methods declared by a single provider, with their activation state.</summary>
public sealed record PaymentProviderConfigurationResponse(
    string ProviderName,
    IReadOnlyList<PaymentMethodConfigurationItem> Methods);

/// <summary>A single provider catalog entry fetched live from the provider.</summary>
/// <param name="MethodType">Stable method identifier.</param>
/// <param name="Category">Grouping category for the checkout UI.</param>
/// <param name="DisplayLabel">Provider-native display label (caller can still localize).</param>
/// <param name="Capability">Live capability metadata reported by the provider.</param>
/// <param name="Activated">Whether the method is currently active on the platform.</param>
/// <param name="HasSnapshot">Whether the activation record already carries a capability snapshot.</param>
public sealed record PaymentCatalogMethod(
    string MethodType,
    PaymentMethodCategory Category,
    string DisplayLabel,
    PaymentMethodCapabilityResponse Capability,
    bool Activated,
    bool HasSnapshot);

/// <summary>Catalog response for a single provider.</summary>
public sealed record PaymentProviderCatalogResponse(
    string ProviderName,
    IReadOnlyList<PaymentCatalogMethod> Methods);

/// <summary>API-facing shape of <see cref="Contracts.PaymentMethodCapability"/>.</summary>
/// <param name="SupportedCountries">ISO-3166 alpha-2 codes. Empty = global.</param>
/// <param name="SupportedCurrencies">ISO-4217 alpha-3 codes. Empty = all.</param>
/// <param name="SupportedSequenceTypes">Sequence mode names: <c>oneoff</c>, <c>first</c>, <c>recurring</c>.</param>
/// <param name="AmountBounds">Per-currency amount bounds.</param>
public sealed record PaymentMethodCapabilityResponse(
    IReadOnlyList<string> SupportedCountries,
    IReadOnlyList<string> SupportedCurrencies,
    IReadOnlyList<string> SupportedSequenceTypes,
    IReadOnlyList<PaymentMethodAmountBoundResponse> AmountBounds);

/// <summary>API-facing shape of <see cref="Domain.PaymentMethodAmountBound"/>.</summary>
/// <param name="CurrencyCode">ISO-4217 alpha-3 code.</param>
/// <param name="MinAmount">Minimum accepted amount, or <see langword="null"/> for no floor.</param>
/// <param name="MaxAmount">Maximum accepted amount, or <see langword="null"/> for no ceiling.</param>
public sealed record PaymentMethodAmountBoundResponse(
    string CurrencyCode,
    decimal? MinAmount,
    decimal? MaxAmount);
