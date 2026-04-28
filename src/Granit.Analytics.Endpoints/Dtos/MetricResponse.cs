using Granit.Analytics.Metrics;

namespace Granit.Analytics.Endpoints.Dtos;

/// <summary>
/// Response envelope for a metric evaluation. Locks the wire format for forward
/// compatibility with the future push transport (delta messages share the same logical
/// schema) — see EPIC #1366 future-proofing invariant #2.
/// </summary>
/// <param name="Name">The metric definition name.</param>
/// <param name="Snapshot">Full state at <paramref name="EmittedAt"/>.</param>
/// <param name="Sequence">
/// Monotonic sequence per (metric, tenant, period). Pull responses always carry sequence
/// <c>1</c> for the most recent computation; future push messages emit incrementing
/// sequences so the frontend can dedup or merge.
/// </param>
/// <param name="EmittedAt">Server-side timestamp of the computation.</param>
/// <param name="RefreshHint">Pull / push transport hint inherited from the metric definition.</param>
public sealed record MetricResponse(
    string Name,
    MetricSnapshotPayload Snapshot,
    long Sequence,
    DateTimeOffset EmittedAt,
    RefreshHint RefreshHint);
