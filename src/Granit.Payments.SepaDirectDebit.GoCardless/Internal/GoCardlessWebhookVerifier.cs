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

        if (!IsSignatureValid(body, signature))
        {
            Log.WebhookRejected(logger, "Invalid signature");
            return Task.FromResult(new PaymentWebhookVerificationResult(
                IsValid: false, EventType: null, ProviderEventId: null,
                Payload: default, RejectionReason: "Invalid webhook signature."));
        }

        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(body);
        (string compositeEventId, string eventType, int batchSize) = ExtractBatchMetadata(payload);

        Log.WebhookVerified(logger, compositeEventId, batchSize);

        return Task.FromResult(new PaymentWebhookVerificationResult(
            IsValid: true,
            EventType: eventType,
            ProviderEventId: compositeEventId,
            Payload: payload,
            RejectionReason: null));
    }

    private bool IsSignatureValid(byte[] body, string signature)
    {
        byte[] key = Encoding.UTF8.GetBytes(options.Value.WebhookSecret);
        byte[] computedHash = HMACSHA256.HashData(key, body);
        string computedSignature = Convert.ToHexStringLower(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    // Extracts a deterministic composite event ID covering all events in the batch.
    // GoCardless sends batched events — using only the first ID would silently lose
    // subsequent events on replay and break deduplication for partial overlaps.
    private static (string CompositeEventId, string EventType, int BatchSize) ExtractBatchMetadata(JsonElement payload)
    {
        if (!payload.TryGetProperty("events", out JsonElement events)
            || events.ValueKind != JsonValueKind.Array
            || events.GetArrayLength() == 0)
        {
            return ("", "gocardless.webhook", 0);
        }

        List<string> eventIds = [];
        string? firstAction = null;

        foreach (JsonElement evt in events.EnumerateArray())
        {
            if (evt.TryGetProperty("id", out JsonElement id) && id.GetString() is string eid)
            {
                eventIds.Add(eid);
            }

            firstAction ??= evt.TryGetProperty("action", out JsonElement action)
                ? action.GetString()
                : null;
        }

        string compositeEventId = eventIds.Count == 1 ? eventIds[0] : string.Join('+', eventIds);
        string eventType = firstAction is not null ? $"gocardless.{firstAction}" : "gocardless.webhook";

        return (compositeEventId, eventType, events.GetArrayLength());
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "GoCardless webhook verified (event: {EventId}, batch size: {BatchSize})")]
        public static partial void WebhookVerified(ILogger logger, string eventId, int batchSize);

        [LoggerMessage(Level = LogLevel.Warning, Message = "GoCardless webhook rejected: {Reason}")]
        public static partial void WebhookRejected(ILogger logger, string reason);
    }
}
