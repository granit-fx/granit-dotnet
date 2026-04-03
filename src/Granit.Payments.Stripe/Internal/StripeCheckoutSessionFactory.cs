using Granit.Payments.Contracts;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of ICheckoutSessionFactory.</summary>
internal sealed class StripeCheckoutSessionFactory : ICheckoutSessionFactory
{
    public string ProviderName => "stripe";

    public Task<PaymentCheckoutSession> CreateAsync(PaymentCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        // TODO: Implement via Stripe Checkout Session API
        throw new NotSupportedException("Stripe checkout not configured.");
    }
}
