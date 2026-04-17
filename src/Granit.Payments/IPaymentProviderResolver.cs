using Granit.Payments.Contracts;

namespace Granit.Payments;

/// <summary>
/// Resolves the payment provider for a tenant and payment method type.
/// A tenant can have multiple providers active (e.g., Stripe for cards + Mollie for iDEAL).
/// </summary>
public interface IPaymentProviderResolver
{
    /// <summary>Resolves the provider for a specific payment method type.</summary>
    Task<IPaymentProvider> ResolveAsync(Guid tenantId, string methodType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all available payment methods for a tenant (for checkout UI). When
    /// <paramref name="context"/> is non-null, methods whose capability snapshot does not
    /// satisfy the context (country / currency / amount / sequence) are filtered out.
    /// Null context returns the unfiltered set.
    /// </summary>
    Task<IReadOnlyList<PaymentAvailableMethod>> GetAvailableProvidersAsync(
        Guid tenantId,
        PaymentAvailabilityContext? context = null,
        CancellationToken cancellationToken = default);
}
