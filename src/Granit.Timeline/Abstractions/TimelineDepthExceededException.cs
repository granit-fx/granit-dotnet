namespace Granit.Timeline.Abstractions;

/// <summary>
/// Thrown by <see cref="ITimelineReader.GetStreamAsync"/> when the requested
/// page exceeds <c>TimelineOptions.MaxPage</c>. Federated pagination cost
/// scales with depth (every page re-fetches a top-(page × pageSize) slice
/// from every source); the cap protects the DB from a misbehaving client.
/// The endpoint maps this to <c>400 timeline-depth-exceeded</c>.
/// </summary>
public sealed class TimelineDepthExceededException(int requestedPage, int maxPage)
    : InvalidOperationException($"Requested page {requestedPage} exceeds the configured maximum of {maxPage}.")
{
    /// <summary>Page index the caller asked for (1-based).</summary>
    public int RequestedPage { get; } = requestedPage;

    /// <summary>Maximum allowed page from <c>TimelineOptions.MaxPage</c>.</summary>
    public int MaxPage { get; } = maxPage;
}
