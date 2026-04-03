using Granit.Payments.Contracts;
using Granit.Payments.Domain;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of IPaymentProvider.</summary>
internal sealed class StripePaymentProvider : IPaymentProvider
{
    public string Name => "stripe";

    public Task<PaymentProviderChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        // TODO: Implement via Stripe PaymentIntent API
        return Task.FromResult(new PaymentProviderChargeResult(
            ProviderTransactionId: string.Empty, Status: ProviderChargeStatus.Failed));
    }

    public Task<PaymentProviderRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentProviderRefundResult(
            ProviderRefundId: string.Empty, Status: RefundStatus.Failed));

    public Task<PaymentProviderStatus> GetStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentProviderStatus(
            ProviderTransactionId: providerTransactionId, Status: PaymentStatus.Failed));
}
