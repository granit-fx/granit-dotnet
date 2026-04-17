using Granit.Payments.Domain;

namespace Granit.Payments.Contracts;

/// <summary>
/// Declares a payment method supported by an <see cref="IPaymentProvider"/>.
/// </summary>
/// <param name="MethodType">
/// Stable identifier for the method (e.g., <see cref="PaymentMethods.Card"/>,
/// <see cref="PaymentMethods.Bancontact"/>). Used as the i18n key for translations
/// under <c>Payments.Methods.{methodType}</c>.
/// </param>
/// <param name="Category">Grouping category for the checkout UI.</param>
public sealed record PaymentMethodDescriptor(string MethodType, PaymentMethodCategory Category);
