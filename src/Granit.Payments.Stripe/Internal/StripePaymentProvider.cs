using Granit.Payments.Dtos;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of IPaymentProvider.</summary>
internal sealed class StripePaymentProvider : IPaymentProvider
{
    public string Name => "stripe";

    public Task<ProviderChargeResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default)
    {
        // TODO: Implement via Stripe PaymentIntent API
        throw new NotImplementedException("Stripe provider implementation pending.");
    }

    public Task<ProviderRefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Stripe provider implementation pending.");
    }

    public Task<ProviderPaymentStatus> GetStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Stripe provider implementation pending.");
    }
}
