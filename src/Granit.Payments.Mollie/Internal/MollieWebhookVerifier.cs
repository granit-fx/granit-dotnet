using System.Text;
using System.Text.Json;
using Granit.Payments.Contracts;
using Microsoft.Extensions.Logging;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models.Payment.Response;

namespace Granit.Payments.Mollie.Internal;

/// <summary>
/// Mollie webhook verification via server-side API callback.
/// </summary>
/// <remarks>
/// <para>
/// Mollie does not sign webhooks with HMAC. Instead, it sends a POST with the payment ID
/// in the body (form-encoded: <c>id=tr_xxx</c>). Verification happens by fetching
/// the payment from the Mollie API to confirm it exists and retrieve its current status.
/// </para>
/// <para>
/// This is Mollie's documented security model: the webhook is only trusted after the
/// server confirms the payment ID against the Mollie API. A forged webhook with a
/// non-existent payment ID is rejected.
/// </para>
/// </remarks>
internal sealed partial class MollieWebhookVerifier(
    IPaymentClient paymentClient,
    ILogger<MollieWebhookVerifier> logger) : IPaymentWebhookVerifier
{
    /// <inheritdoc/>
    public string ProviderName => "mollie";

    /// <inheritdoc/>
    public async Task<PaymentWebhookVerificationResult> VerifyAsync(
        byte[] body, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        string bodyString = Encoding.UTF8.GetString(body);
        string? paymentId = ExtractPaymentId(bodyString);

        if (string.IsNullOrEmpty(paymentId))
        {
            Log.WebhookRejected(logger, "Could not extract payment ID from body");
            return new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: "Could not extract payment ID from body.");
        }

        // Server-side verification: confirm payment exists via Mollie API
        PaymentResponse molliePayment;
        try
        {
            molliePayment = await paymentClient
                .GetPaymentAsync(paymentId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.WebhookVerificationFailed(logger, paymentId, ex.Message);
            return new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: "Payment verification failed.");
        }

        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            id = molliePayment.Id,
            status = molliePayment.Status,
        });

        Log.WebhookVerified(logger, molliePayment.Id, molliePayment.Status);

        return new PaymentWebhookVerificationResult(
            IsValid: true,
            EventType: $"payment.{molliePayment.Status}",
            ProviderEventId: molliePayment.Id,
            Payload: payload,
            RejectionReason: null);
    }

    private static string? ExtractPaymentId(string body) =>
        body.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] == "id")
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .FirstOrDefault();

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Mollie webhook verified: {PaymentId} (status: {Status})")]
        public static partial void WebhookVerified(ILogger logger, string paymentId, string status);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Mollie webhook rejected: {Reason}")]
        public static partial void WebhookRejected(ILogger logger, string reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Mollie webhook verification failed for {PaymentId}: {Error}")]
        public static partial void WebhookVerificationFailed(ILogger logger, string paymentId, string error);
    }
}
