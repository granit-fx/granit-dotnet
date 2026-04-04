using Granit.Notifications.Domain;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Audit trail for notification delivery attempts (ISO 27001 compliance).
/// </summary>
/// <remarks>
/// Records are INSERT-only during the HDS retention period (default 3 years).
/// After retention expires, <see cref="DeleteBeforeAsync"/> enables RGPD-compliant
/// data minimization by purging obsolete records in batches.
/// </remarks>
public interface INotificationDeliveryWriter
{
    /// <summary>Checks whether a delivery with the given ID has already been successfully recorded.</summary>
    Task<bool> HasBeenDeliveredAsync(Guid deliveryId, CancellationToken cancellationToken = default);

    /// <summary>Records a single delivery attempt (INSERT-only, immutable).</summary>
    Task RecordAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes delivery attempts that occurred before <paramref name="cutoff"/>,
    /// limited to <paramref name="batchSize"/> rows per invocation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is intended for post-retention cleanup only. The caller must ensure
    /// that <paramref name="cutoff"/> respects the HDS retention period (typically 3 years)
    /// before invoking this method.
    /// </para>
    /// <para>
    /// ISO 27001: deletion is permitted after the mandatory retention period has elapsed.
    /// RGPD Art. 5(1)(e): storage limitation requires deletion of data no longer necessary.
    /// </para>
    /// </remarks>
    /// <param name="cutoff">Only attempts that occurred before this instant are deleted.</param>
    /// <param name="batchSize">Maximum number of rows to delete per invocation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows actually deleted.</returns>
    Task<int> DeleteBeforeAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}
