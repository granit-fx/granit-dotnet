using Granit.Payments.Dtos;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of ICheckoutSessionFactory.</summary>
internal sealed class StripeCheckoutSessionFactory : ICheckoutSessionFactory
{
    public string ProviderName => "stripe";

    public Task<CheckoutSession> CreateAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        // TODO: Implement via Stripe Checkout Session API
        throw new NotImplementedException("Stripe checkout implementation pending.");
    }
}
