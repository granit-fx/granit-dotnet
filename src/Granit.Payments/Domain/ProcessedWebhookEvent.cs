using Granit.Domain;

namespace Granit.Payments.Domain;

/// <summary>
/// Append-only record for inbound webhook deduplication.
/// Unique constraint on (ProviderName, ProviderEventId).
/// </summary>
public sealed class ProcessedWebhookEvent : Entity
{
    private ProcessedWebhookEvent() { }

    /// <summary>Creates a new processed webhook event record.</summary>
    public static ProcessedWebhookEvent Create(
        Guid id, string providerName, string providerEventId,
        string eventType, DateTimeOffset processedAt) =>
        new()
        {
            Id = id,
            ProviderName = providerName,
            ProviderEventId = providerEventId,
            EventType = eventType,
            ProcessedAt = processedAt,
        };

    public string ProviderName { get; private set; } = string.Empty;
    public string ProviderEventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }
}
