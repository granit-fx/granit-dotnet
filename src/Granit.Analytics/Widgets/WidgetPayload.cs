using Granit.Analytics.Metrics;
using Granit.Dashboards;

namespace Granit.Analytics.Widgets;

/// <summary>
/// Canonical envelope for any widget payload returned by an <see cref="IWidgetSource{TPayload}"/>.
/// Locks the schema so that future push (delta) messages compose with v1 pull (snapshot)
/// responses without breaking the wire format.
/// </summary>
/// <typeparam name="TSnapshot">The payload-specific snapshot shape (e.g. metric value, chart series).</typeparam>
/// <param name="Snapshot">The full state at <paramref name="EmittedAt"/>.</param>
/// <param name="Sequence">
/// Monotonic sequence per (widget identity, tenant). Pull responses always carry sequence <c>1</c>
/// for the most recent computation; push messages emit incrementing sequences so the frontend can
/// merge or dedup.
/// </param>
/// <param name="EmittedAt">Server-side timestamp of the computation.</param>
/// <param name="RefreshHint">Pull / push transport hint inherited from the underlying definition.</param>
/// <remarks>
/// Per EPIC #1366 future-proofing invariant #2 — pull <c>{ Snapshot, Sequence, EmittedAt }</c>
/// and push <c>{ Sequence, Delta }</c> share the same logical schema. The frontend hook
/// (<c>useMetric</c>, <c>useWidget</c>) merges them transparently when push lands.
/// </remarks>
public sealed record WidgetPayload<TSnapshot>(
    TSnapshot Snapshot,
    long Sequence,
    DateTimeOffset EmittedAt,
    RefreshHint RefreshHint);
