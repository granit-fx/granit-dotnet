using Granit.QueryEngine;

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
}
