using Granit.Payments.Contracts;

namespace Granit.Payments;

/// <summary>Verifies inbound webhook signatures from a payment provider.</summary>
public interface IPaymentWebhookVerifier
{
    /// <summary>Provider name this verifier handles.</summary>
    string ProviderName { get; }

    /// <summary>Verifies the webhook signature and parses the payload.</summary>
    /// <param name="body">Raw request body bytes.</param>
    /// <param name="headers">Request headers (for signature validation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PaymentWebhookVerificationResult> VerifyAsync(
        byte[] body,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
