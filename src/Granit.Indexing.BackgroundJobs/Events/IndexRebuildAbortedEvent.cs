using Granit.Events;

namespace Granit.Indexing.BackgroundJobs.Events;

/// <summary>
/// Raised when a rebuild run is cut short before reaching end-of-stream — by the
/// consecutive-failure circuit-breaker, by a budget cutoff, or by cancellation.
/// The checkpoint is preserved so a re-dispatch resumes. Local (<see cref="IDomainEvent"/>).
/// </summary>
/// <param name="TenantId">Target tenant. <c>null</c> = cross-tenant rebuild.</param>
/// <param name="SourceName"><see cref="IIndexedEntrySource{TKey}.Name"/> being rebuilt.</param>
/// <param name="KeyTypeName">Closed generic <c>TKey</c> discriminator.</param>
/// <param name="Reason">
/// Stable cause identifier. One of: <c>"max_consecutive_failures"</c>,
/// <c>"max_entries_per_run"</c>, <c>"max_run_duration"</c>, <c>"cancelled"</c>.
/// </param>
/// <param name="EntriesProcessed">Total entries processed before the cutoff (indexed + skipped + failed).</param>
/// <param name="DispatchedByUserId">Dispatching principal's user id, when available.</param>
public sealed record IndexRebuildAbortedEvent(
    Guid? TenantId,
    string SourceName,
    string KeyTypeName,
    string Reason,
    long EntriesProcessed,
    string? DispatchedByUserId) : IDomainEvent;
