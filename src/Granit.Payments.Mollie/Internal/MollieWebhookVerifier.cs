using Granit.Payments.Dtos;

namespace Granit.Payments.Mollie.Internal;

internal sealed class MollieWebhookVerifier : IPaymentWebhookVerifier
{
    public string ProviderName => "mollie";

    public Task<WebhookVerificationResult> VerifyAsync(
        byte[] body, IDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Mollie webhook verification pending.");
}
