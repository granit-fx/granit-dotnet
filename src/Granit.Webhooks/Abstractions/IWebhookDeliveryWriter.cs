using Granit.Webhooks.Messages;

namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Stores webhook delivery attempt records for the ISO 27001 audit trail.
/// </summary>
/// <remarks>
/// ISO 27001 requirement: every delivery attempt must be recorded and retained for 3 years.
/// The default registration is <c>NullWebhookDeliveryWriter</c> (no-op, for development and tests).
/// Production applications must call <c>AddGranitWebhooksEntityFrameworkCore()</c> to enable
/// durable persistence.
/// </remarks>
public interface IWebhookDeliveryWriter
{
    /// <summary>
    /// Records a successful (2xx) delivery attempt.
    /// Also resets the subscription's consecutive failure counter and updates <c>LastSuccessAt</c>.
    /// </summary>
    Task RecordSuccessAsync(
        SendWebhookCommand command,
        int httpStatusCode,
        long durationMs,
        string payloadHash,
        string? payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed delivery attempt and increments the subscription's consecutive failure counter.
    /// </summary>
    Task RecordFailureAsync(
        SendWebhookCommand command,
        int? httpStatusCode,
        long durationMs,
        string errorMessage,
        string? payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a subscription after a non-retriable HTTP error.
    /// Sets <c>Status = Suspended</c>, records <c>SuspendedAt</c> and the <paramref name="reason"/>.
    /// </summary>
    Task SuspendSubscriptionAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes delivery attempts that occurred before <paramref name="cutoff"/>,
    /// up to <paramref name="batchSize"/> rows per call.
    /// </summary>
    /// <remarks>
    /// GDPR Art. 5(1)(e) data minimisation: after the ISO 27001 retention period (3 years),
    /// delivery records must be purged. Called by archival background jobs in a batch-delete loop.
    /// </remarks>
    /// <returns>The number of rows actually deleted.</returns>
    Task<int> DeleteBeforeAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}
