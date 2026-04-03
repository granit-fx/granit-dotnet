using Granit.Payments.Contracts;

namespace Granit.Payments.Mollie.Internal;

internal sealed class MollieWebhookVerifier : IPaymentWebhookVerifier
{
    public string ProviderName => "mollie";

    public Task<PaymentWebhookVerificationResult> VerifyAsync(
        byte[] body, IDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentWebhookVerificationResult(
            IsValid: false, EventType: null, ProviderEventId: null,
            Payload: default, RejectionReason: "Mollie webhook verification not yet implemented."));
}
