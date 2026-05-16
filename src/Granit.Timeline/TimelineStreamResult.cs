using Granit.QueryEngine;

namespace Granit.Timeline;

/// <summary>
/// Result of <c>ITimelineReader.GetStreamAsync</c>: the paged set of merged
/// entries plus the list of registered <c>ITimelineSource</c> contributors
/// that were dropped from the response (timeout or thrown) under the
/// <see cref="Options.SourceFailurePolicy.DegradeGracefully"/> policy.
/// </summary>
/// <param name="Page">Merged, deduplicated, sorted page of entries.</param>
/// <param name="DegradedSources">
/// Source keys that failed during this call. Empty when every contributor
/// answered within <c>TimelineOptions.SourceTimeout</c>. The endpoint surfaces
/// this list via the <c>X-Timeline-Degraded-Sources</c> response header so the
/// client can render a "partial data" indicator.
/// </param>
public sealed record TimelineStreamResult(
    PagedResult<TimelineStreamEntry> Page,
    IReadOnlyList<string> DegradedSources);
