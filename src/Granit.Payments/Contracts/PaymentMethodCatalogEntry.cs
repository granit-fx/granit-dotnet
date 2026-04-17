using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>
/// Describes a payment method as seen by a provider at a given point in time.
/// </summary>
/// <remarks>
/// Returned by <see cref="IPaymentProvider.GetCatalogAsync"/>. The admin activation flow
/// uses this shape to populate the capability snapshot on
/// <c>PaymentMethodConfiguration</c>. The runtime filter operates against the persisted
/// snapshot, never against the live catalog.
/// </remarks>
/// <param name="MethodType">Stable method identifier (e.g., <c>card</c>, <c>bancontact</c>).</param>
/// <param name="Category">Grouping category for the checkout UI.</param>
/// <param name="DisplayLabel">Provider-native label (English by default). Callers may still localize via <c>IStringLocalizer</c>.</param>
/// <param name="Capability">Availability metadata for this method.</param>
public sealed record PaymentMethodCatalogEntry(
    string MethodType,
    PaymentMethodCategory Category,
    string DisplayLabel,
    PaymentMethodCapability Capability);
