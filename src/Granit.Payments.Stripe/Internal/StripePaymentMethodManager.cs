using Granit.Payments.Contracts;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of IPaymentMethodManager.</summary>
internal sealed class StripePaymentMethodManager : IPaymentMethodManager
{
    public string ProviderName => "stripe";

    public Task<IReadOnlyList<PaymentProviderMethod>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Stripe payment method management not configured.");

    public Task<PaymentProviderMethod> AttachAsync(PaymentAttachMethodRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Stripe payment method management not configured.");

    public Task DetachAsync(string providerMethodId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Stripe payment method management not configured.");
}
