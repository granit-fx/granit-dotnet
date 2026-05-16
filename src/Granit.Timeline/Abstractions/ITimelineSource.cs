namespace Granit.Timeline.Abstractions;

/// <summary>
/// Contributes entries to the federated activity stream from an external store
/// (audit log, workflow history, notifications, …). Implementations live in
/// bridge packages (e.g. <c>Granit.Timeline.Auditing</c>) so the base Timeline
/// module stays free of cross-module dependencies.
/// </summary>
/// <remarks>
/// <para>
/// The reader fans out to every registered <see cref="ITimelineSource"/>,
/// merges results with the native <c>TimelineEntries</c> table, and applies a
/// deterministic tiebreak (newest first, native wins ties, then
/// <see cref="SourceKey"/> ASC, then <c>Id</c> ASC).
/// </para>
/// <para>
/// Each implementation is responsible for its own tenant scoping and read
/// authorization — the bridge typically reuses the same reader the source
/// module already exposes (e.g. <c>IAuditingReader</c>).
/// </para>
/// </remarks>
public interface ITimelineSource
{
    /// <summary>
    /// Source identifier returned on every emitted <see cref="TimelineStreamEntry.SourceKey"/>.
    /// Must match <see cref="TimelineSourceKeys.IsValid(string)"/>, must be unique across
    /// registered sources, and must never be <see cref="TimelineSourceKeys.Native"/>.
    /// </summary>
    string SourceKey { get; }

    /// <summary>
    /// Returns up to <paramref name="limit"/> most recent entries for the given entity,
    /// ordered by occurrence DESC. The reader fetches <c>(page × pageSize + buffer)</c>
    /// per source then merges in memory.
    /// </summary>
    /// <remarks>
    /// Implementations should honor <paramref name="cancellationToken"/> aggressively —
    /// the reader wraps every call in a per-source timeout
    /// (<c>TimelineOptions.SourceTimeout</c>) and degrades the response when a source
    /// blows past it.
    /// </remarks>
    Task<IReadOnlyList<TimelineStreamEntry>> GetEntriesAsync(
        string entityType,
        string entityId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the projected entry for a single <paramref name="sourceId"/>, or
    /// <see langword="null"/> if it no longer exists in the source store.
    /// Called when materializing a shadow row at anchor time so the snapshot
    /// uses the same projection as the read path.
    /// </summary>
    Task<TimelineStreamEntry?> GetEntryAsync(
        string entityType,
        string entityId,
        string sourceId,
        CancellationToken cancellationToken = default);
}
