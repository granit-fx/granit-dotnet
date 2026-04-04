using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Payments.Contracts;
using Granit.Payments.SepaDirectDebit.GoCardless.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Payments.SepaDirectDebit.GoCardless.Internal;

/// <summary>GoCardless webhook signature verification (HMAC-SHA256).</summary>
internal sealed partial class GoCardlessWebhookVerifier(
    IOptions<GoCardlessOptions> options,
    ILogger<GoCardlessWebhookVerifier> logger) : IPaymentWebhookVerifier
{
    /// <inheritdoc/>
    public string ProviderName => "gocardless";

    /// <inheritdoc/>
    public Task<PaymentWebhookVerificationResult> VerifyAsync(
        byte[] body, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (!headers.TryGetValue("Webhook-Signature", out string? signature) || string.IsNullOrEmpty(signature))
        {
            return Task.FromResult(new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: "Missing Webhook-Signature header."));
        }

        // HMAC-SHA256 verification
        byte[] key = Encoding.UTF8.GetBytes(options.Value.WebhookSecret);
        byte[] computedHash = HMACSHA256.HashData(key, body);
        string computedSignature = Convert.ToHexStringLower(computedHash);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signature)))
        {
            Log.WebhookRejected(logger, "Invalid signature");
            return Task.FromResult(new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: "Invalid webhook signature."));
        }

        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(body);

        // Extract event ID from the payload for deduplication
        string? eventId = payload.TryGetProperty("events", out JsonElement events)
            && events.ValueKind == JsonValueKind.Array
            && events.GetArrayLength() > 0
            && events[0].TryGetProperty("id", out JsonElement id)
                ? id.GetString()
                : null;

        string? eventType = payload.TryGetProperty("events", out JsonElement evts)
            && evts.ValueKind == JsonValueKind.Array
            && evts.GetArrayLength() > 0
            && evts[0].TryGetProperty("action", out JsonElement action)
                ? $"gocardless.{action.GetString()}"
                : "gocardless.webhook";

        Log.WebhookVerified(logger, eventId);

        return Task.FromResult(new PaymentWebhookVerificationResult(
            IsValid: true,
            EventType: eventType,
            ProviderEventId: eventId,
            Payload: payload,
            RejectionReason: null));
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "GoCardless webhook verified (event: {EventId})")]
        public static partial void WebhookVerified(ILogger logger, string? eventId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "GoCardless webhook rejected: {Reason}")]
        public static partial void WebhookRejected(ILogger logger, string reason);
    }
}
