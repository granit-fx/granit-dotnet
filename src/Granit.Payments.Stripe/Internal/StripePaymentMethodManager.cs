using Granit.Payments.Dtos;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of IPaymentMethodManager.</summary>
internal sealed class StripePaymentMethodManager : IPaymentMethodManager
{
    public string ProviderName => "stripe";

    public Task<IReadOnlyList<ProviderPaymentMethod>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Stripe payment method implementation pending.");
    }

    public Task<ProviderPaymentMethod> AttachAsync(AttachPaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Stripe payment method implementation pending.");
    }

    public Task DetachAsync(string providerMethodId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Stripe payment method implementation pending.");
    }
}
