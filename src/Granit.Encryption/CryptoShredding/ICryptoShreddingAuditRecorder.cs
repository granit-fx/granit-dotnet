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
    /// Records that a per-entity encryption key was permanently destroyed.
    /// </summary>
    /// <param name="entityType">Logical entity type name.</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="shreddedAt">Timestamp of the shredding operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(
        string entityType,
        string entityId,
        DateTimeOffset shreddedAt,
        CancellationToken cancellationToken = default);
}
