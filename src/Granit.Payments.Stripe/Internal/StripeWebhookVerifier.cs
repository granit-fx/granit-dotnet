using Granit.Payments.Contracts;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of IPaymentWebhookVerifier (HMAC-SHA256 signature validation).</summary>
internal sealed class StripeWebhookVerifier : IPaymentWebhookVerifier
{
    public string ProviderName => "stripe";

    public Task<PaymentWebhookVerificationResult> VerifyAsync(
        byte[] body, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        // TODO: Implement via Stripe EventUtility.ConstructEventAsync
        // Validate Stripe-Signature header (t=<timestamp>,v1=<hmac>)
        // Reject timestamps older than 5 minutes (replay protection)
        return Task.FromResult(new PaymentWebhookVerificationResult(
            IsValid: false, EventType: null, ProviderEventId: null,
            Payload: default, RejectionReason: "Stripe webhook verification not yet implemented."));
    }
}
