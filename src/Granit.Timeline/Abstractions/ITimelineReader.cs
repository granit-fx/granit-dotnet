using Granit.QueryEngine;
using Granit.Timeline.Domain;
using Granit.Timeline.Exceptions;

namespace Granit.Timeline.Abstractions;

/// <summary>
/// Read operations for the federated activity stream.
/// Returns a paginated, chronologically merged view that combines the native
/// <c>TimelineEntries</c> table with every registered <see cref="ITimelineSource"/>.
/// </summary>
public interface ITimelineReader
{
    /// <summary>
    /// Returns a paginated activity stream for a specific entity. Entries are
    /// ordered <c>OccurredAt</c> DESC, with native rows winning ties over
    /// external-source projections (then <c>SourceKey</c> ASC, then <c>Id</c>
    /// ASC) for stable infinite-scroll pagination.
    /// </summary>
    /// <exception cref="TimelineDepthExceededException">
    /// Thrown when <paramref name="page"/> exceeds <c>TimelineOptions.MaxPage</c>.
    /// </exception>
    Task<TimelineStreamResult> GetStreamAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single native timeline entry projected as a <see cref="TimelineStreamEntry"/>,
    /// or <see langword="null"/> when no matching entry exists (wrong entity, soft-deleted, or
    /// unknown id). Scoped to the current tenant by the same query filter as
    /// <see cref="GetStreamAsync"/>. Used for targeted lookups such as ownership checks, where
    /// scanning a page would be both wasteful and incorrect beyond the page bound.
    /// </summary>
    Task<TimelineStreamEntry?> GetEntryAsync(
        string entityType,
        string entityId,
        Guid entryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the raw <see cref="TimelineEntry"/> aggregate for a single entry id,
    /// or <see langword="null"/> when no matching entry exists (soft-deleted or unknown id).
    /// Used for internal orchestration (e.g. reaction notifications) where the caller only
    /// has the entry id — unlike <see cref="GetEntryAsync"/>, no entity scoping is required.
    /// Scoped to the current tenant by the same query filter as <see cref="GetStreamAsync"/>.
    /// </summary>
    Task<TimelineEntry?> GetByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);
}
