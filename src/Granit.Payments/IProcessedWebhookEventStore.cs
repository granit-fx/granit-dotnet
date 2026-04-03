namespace Granit.Payments;

/// <summary>
/// Webhook deduplication store. Insert-first pattern with unique constraint catch.
/// </summary>
public interface IProcessedWebhookEventStore
{
    /// <summary>
    /// Attempts to record a webhook event. Returns <c>true</c> if recorded (new),
    /// <c>false</c> if duplicate (already processed).
    /// </summary>
    Task<bool> TryRecordAsync(
        string providerName, string providerEventId, string eventType,
        CancellationToken cancellationToken = default);
}
