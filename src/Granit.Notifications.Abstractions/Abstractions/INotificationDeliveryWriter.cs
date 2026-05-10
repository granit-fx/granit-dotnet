using Granit.Notifications.Domain;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Audit trail for notification delivery attempts (ISO 27001 compliance).
/// </summary>
/// <remarks>
/// <para>
/// Each outbound delivery identifies a canonical <see cref="NotificationDeliveryAttempt.DeliveryId"/>.
/// Persisted rows persist through retention (default three years); <see cref="DeleteBeforeAsync"/>
/// enables GDPR-compatible batch purges after cutoff.
/// </para>
/// <para>
/// Claim/finalization ensures at most one SMTP (or SMS, etc.) send per durable attempt id despite
/// parallel dispatch or retry loops (see GH #947).
/// </para>
/// </remarks>
public interface INotificationDeliveryWriter
{
    /// <summary>Checks whether a delivery with the given ID has already been successfully recorded.</summary>
    Task<bool> HasBeenDeliveredAsync(Guid deliveryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to claim exclusive responsibility for attempting this delivery row.
    /// </summary>
    /// <remarks>
    /// Insertion uses <see cref="NotificationDeliveryAttempt.IsSuccess"/><c> == null</c> for the in-flight
    /// claim. Duplicate keys (race) or overlapping workers return <see langword="false"/>.
    /// If this delivery previously failed terminally (<see cref="NotificationDeliveryAttempt.IsSuccess"/>
    /// is <see langword="false"/> — e.g., channel outage), the failure row is transitioned back to
    /// pending so the transport-layer retry can legally re-issue the outbound send exactly once again.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> when the caller should invoke the outbound channel immediately;
    /// <see langword="false"/> when another worker owns an in-flight terminal state.
    /// </returns>
    Task<bool> TryAcquireDeliveryAttemptAsync(
        NotificationDeliveryAttempt claim,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the terminal audit fields (<see cref="NotificationDeliveryAttempt.IsSuccess"/>)
    /// for a claimed row keyed by <paramref name="deliveryId"/>.
    /// </summary>
    /// <remarks>
    /// Persist failures after a successful SMTP send must be swallowed upstream so retries do not
    /// duplicate user-visible messages (#947): this method succeeds as a best-effort no-op when no
    /// matching in-flight (<c>NULL</c> success) rows exist.
    /// </remarks>
    Task CompleteDeliveryAttemptAsync(
        Guid deliveryId,
        bool success,
        long durationMilliseconds,
        string? errorMessage,
        CancellationToken cancellationToken = default);

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
    /// GDPR Art. 5(1)(e): storage limitation requires deletion of data no longer necessary.
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
