using Granit.QueryEngine;

namespace Granit.Timeline.Abstractions;

/// <summary>
/// Read operations for the unified activity stream.
/// Returns paginated, chronologically ordered entries for a given entity.
/// </summary>
public interface ITimelineReader
{
    /// <summary>
    /// Returns a paginated activity stream for a specific entity.
    /// Entries are ordered by <c>OccurredAt</c> descending (newest first).
    /// </summary>
    Task<PagedResult<TimelineStreamEntry>> GetStreamAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default);
}
