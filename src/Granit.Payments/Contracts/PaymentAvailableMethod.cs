using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>Available payment method for checkout UI.</summary>
/// <param name="MethodType">Stable method identifier.</param>
/// <param name="Category">Grouping category for the checkout UI.</param>
/// <param name="ProviderName">Provider that will process charges for this method.</param>
/// <param name="DisplayLabel">Raw method type — callers localize via <c>IStringLocalizer</c>.</param>
/// <param name="Capability">Capability snapshot, or <see langword="null"/> when no snapshot is available.</param>
public sealed record PaymentAvailableMethod(
    string MethodType,
    PaymentMethodCategory Category,
    string ProviderName,
    string DisplayLabel,
    PaymentMethodCapability? Capability);
