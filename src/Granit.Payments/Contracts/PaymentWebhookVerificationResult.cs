using System.Text.Json;

namespace Granit.Payments.Contracts;

/// <summary>Webhook verification result.</summary>
public sealed record PaymentWebhookVerificationResult(
    bool IsValid, string? EventType, string? ProviderEventId,
    JsonElement Payload, string? RejectionReason);
