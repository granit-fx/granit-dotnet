using System.Text;
using System.Text.Json;
using Granit.Payments.Contracts;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.Mollie.Internal;

/// <summary>
/// Mollie webhook verification.
/// </summary>
/// <remarks>
/// Mollie does not sign webhooks with HMAC. Instead, it sends a POST with the payment ID
/// in the body (form-encoded: <c>id=tr_xxx</c>). Actual verification happens by fetching
/// the payment status from the Mollie API in the webhook handler.
/// </remarks>
internal sealed partial class MollieWebhookVerifier(
    ILogger<MollieWebhookVerifier> logger) : IPaymentWebhookVerifier
{
    /// <inheritdoc/>
    public string ProviderName => "mollie";

    /// <inheritdoc/>
    public Task<PaymentWebhookVerificationResult> VerifyAsync(
        byte[] body, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        string bodyString = Encoding.UTF8.GetString(body);
        string? paymentId = ExtractPaymentId(bodyString);

        if (string.IsNullOrEmpty(paymentId))
        {
            Log.WebhookRejected(logger, "Could not extract payment ID from body");
            return Task.FromResult(new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: "Could not extract payment ID from body."));
        }

        JsonElement payload = JsonSerializer.SerializeToElement(new { id = paymentId });

        Log.WebhookAccepted(logger, paymentId);

        return Task.FromResult(new PaymentWebhookVerificationResult(
            IsValid: true,
            EventType: "payment.status_changed",
            ProviderEventId: paymentId,
            Payload: payload,
            RejectionReason: null));
    }

    private static string? ExtractPaymentId(string body) =>
        body.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] == "id")
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .FirstOrDefault();

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Mollie webhook accepted for payment: {PaymentId}")]
        public static partial void WebhookAccepted(ILogger logger, string paymentId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Mollie webhook rejected: {Reason}")]
        public static partial void WebhookRejected(ILogger logger, string reason);
    }
}
