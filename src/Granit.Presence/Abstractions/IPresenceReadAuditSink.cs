namespace Granit.Presence.Abstractions;

/// <summary>
/// Receives one event per call to the presence query endpoints. Provides the audit
/// seam apps need to investigate scraping / harassment / stalking abuse cases without
/// exploding audit volume on the per-target axis.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation writes a structured ILogger entry. Apps that need
/// durable audit records (ISO 27001 A.5.34, GDPR Art. 30) can replace this with an
/// implementation that writes to <c>Granit.Auditing</c>.
/// </para>
/// <para>
/// Implementations MUST NOT enumerate the targets by logging each target identifier —
/// that would inflate audit volume linearly with the batch size. Counts and the caller
/// identity are sufficient for post-hoc investigation.
/// </para>
/// </remarks>
public interface IPresenceReadAuditSink
{
    /// <summary>
    /// Records a single presence read by <paramref name="callerUserId"/>.
    /// </summary>
    /// <param name="callerUserId">
    /// Caller's user identifier, or <see cref="Guid.Empty"/> when the identity could not
    /// be resolved.
    /// </param>
    /// <param name="targetCount">Number of target users requested.</param>
    /// <param name="allowedCount">Number of targets the visibility policy allowed.</param>
    /// <param name="includesSelf">Whether the call included the caller's own presence.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordReadAsync(
        Guid callerUserId,
        int targetCount,
        int allowedCount,
        bool includesSelf,
        CancellationToken cancellationToken);
}
