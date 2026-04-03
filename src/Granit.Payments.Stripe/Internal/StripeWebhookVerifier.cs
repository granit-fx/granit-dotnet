using System.Text;
using System.Text.Json;
using Granit.Payments.Contracts;
using Granit.Payments.Stripe.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Stripe webhook signature verification using <see cref="EventUtility"/>.
/// Validates HMAC-SHA256 signature and 5-minute timestamp tolerance.
/// </summary>
internal sealed partial class StripeWebhookVerifier(
    IOptions<StripeOptions> options,
    ILogger<StripeWebhookVerifier> logger) : IPaymentWebhookVerifier
{
    /// <inheritdoc/>
    public string ProviderName => "stripe";

    /// <inheritdoc/>
    public Task<PaymentWebhookVerificationResult> VerifyAsync(
        byte[] body, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (!headers.TryGetValue("Stripe-Signature", out string? signature) || string.IsNullOrEmpty(signature))
        {
            return Task.FromResult(new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: "Missing Stripe-Signature header."));
        }

        try
        {
            string json = Encoding.UTF8.GetString(body);
            Event stripeEvent = EventUtility.ConstructEvent(
                json, signature, options.Value.WebhookSecret);

            JsonElement payload = JsonSerializer.Deserialize<JsonElement>(json);

            Log.WebhookVerified(logger, stripeEvent.Id, stripeEvent.Type);

            return Task.FromResult(new PaymentWebhookVerificationResult(
                IsValid: true,
                EventType: stripeEvent.Type,
                ProviderEventId: stripeEvent.Id,
                Payload: payload,
                RejectionReason: null));
        }
        catch (StripeException ex)
        {
            Log.WebhookRejected(logger, ex.Message);
            return Task.FromResult(new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: ex.Message));
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe webhook verified: {EventId} ({EventType})")]
        public static partial void WebhookVerified(ILogger logger, string eventId, string eventType);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe webhook rejected: {Reason}")]
        public static partial void WebhookRejected(ILogger logger, string reason);
    }
}
