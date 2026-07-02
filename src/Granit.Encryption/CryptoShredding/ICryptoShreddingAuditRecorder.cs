namespace Granit.Encryption.CryptoShredding;

/// <summary>
/// Records crypto-shredding events for audit trail compliance (ISO 27001 A.10.1.2).
/// </summary>
/// <remarks>
/// <para>
/// Implementations typically write to <c>ITimelineWriter</c> with
/// <c>TimelineEntryType.SystemLog</c> for immutable audit trail entries.
/// The interface lives in <c>Granit.Encryption</c> to avoid coupling
/// the encryption module to <c>Granit.Timeline</c>.
/// </para>
/// <para>
/// Register implementations via DI:
/// <code>
/// services.AddScoped&lt;ICryptoShreddingAuditRecorder, TimelineCryptoShreddingRecorder&gt;();
/// </code>
/// </para>
/// </remarks>
public interface ICryptoShreddingAuditRecorder
{
    /// <summary>
    /// Records a phase of a crypto-shredding operation.
    /// </summary>
    /// <remarks>
    /// Called twice per erasure: once with <see cref="CryptoShreddingPhase.Requested"/> before the key
    /// is destroyed (durable intent), and once with <see cref="CryptoShreddingPhase.Confirmed"/> after
    /// destruction. Implementations must ensure the <see cref="CryptoShreddingPhase.Requested"/> record
    /// is durable before returning, so the irreversible destruction is never trail-less (GDPR Art. 5(2)).
    /// </remarks>
    /// <param name="entityType">Logical entity type name.</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="phase">Which phase of the two-phase erasure this record captures.</param>
    /// <param name="occurredAt">Timestamp of the shredding operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(
        string entityType,
        string entityId,
        CryptoShreddingPhase phase,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
