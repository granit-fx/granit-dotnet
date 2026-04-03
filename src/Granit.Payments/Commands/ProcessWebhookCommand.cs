using System.Text.Json;

namespace Granit.Payments.Commands;

/// <summary>Command to process an inbound webhook from a payment provider.</summary>
public sealed record ProcessWebhookCommand(
    string ProviderName, string EventType,
    string ProviderEventId, JsonElement Payload);
