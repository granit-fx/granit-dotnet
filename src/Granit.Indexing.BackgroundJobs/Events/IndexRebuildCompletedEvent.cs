using Granit.Events;

namespace Granit.Indexing.BackgroundJobs.Events;

/// <summary>
/// Raised when a rebuild run reaches end-of-stream successfully and clears its
/// checkpoint. Local (<see cref="IDomainEvent"/>).
/// </summary>
/// <param name="TenantId">Target tenant. <c>null</c> = cross-tenant rebuild.</param>
/// <param name="SourceName"><see cref="IIndexedEntrySource{TKey}.Name"/> rebuilt.</param>
/// <param name="KeyTypeName">Closed generic <c>TKey</c> discriminator.</param>
/// <param name="EntriesIndexed">Per-entry indexing successes during the run.</param>
/// <param name="EntriesSkipped">Per-entry skips (resource no longer exists at source).</param>
/// <param name="EntriesFailed">Per-entry failures (build error or indexer fault).</param>
/// <param name="DispatchedByUserId">Dispatching principal's user id, when available.</param>
public sealed record IndexRebuildCompletedEvent(
    Guid? TenantId,
    string SourceName,
    string KeyTypeName,
    long EntriesIndexed,
    long EntriesSkipped,
    long EntriesFailed,
    string? DispatchedByUserId) : IDomainEvent;
