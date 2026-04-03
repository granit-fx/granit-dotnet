using Granit.Payments.Contracts;
using Granit.Payments.Domain;

namespace Granit.Payments;

/// <summary>
/// Resolves the payment provider for a tenant and payment method type.
/// A tenant can have multiple providers active (e.g., Stripe for cards + SEPA for transfers).
/// </summary>
public interface IPaymentProviderResolver
{
    /// <summary>Resolves the provider for a specific payment method type.</summary>
    IPaymentProvider Resolve(Guid tenantId, PaymentMethodType methodType);

    /// <summary>Returns all available payment methods for a tenant (for checkout UI).</summary>
    IReadOnlyList<PaymentAvailableMethod> GetAvailableProviders(Guid tenantId);
}
