using Granit.Analytics.Dashboards.Widgets;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// Wire-shape snapshot for the <c>"Chart"</c> widget kind. Echoes the
/// declarative configuration (<see cref="ChartType"/>, <see cref="GroupBy"/>,
/// <see cref="Aggregation"/>) so the frontend renders without re-reading the
/// widget config, and ships one <see cref="ChartBucket"/> per group as the
/// data series.
/// </summary>
/// <param name="ChartType">Visual hint inherited from <see cref="ChartWidgetDefinition.ChartType"/>.</param>
/// <param name="GroupBy">Group-by field name echoed for the frontend's axis label.</param>
/// <param name="Aggregation">Aggregation applied per bucket — drives the value-axis label.</param>
/// <param name="Field">Aggregated field; <see langword="null"/> for <see cref="AggregateFunction.Count"/>.</param>
/// <param name="Buckets">Group-aggregate series. Order is preserved as the underlying group-by produces it.</param>
public sealed record ChartWidgetSnapshot(
    ChartType ChartType,
    string GroupBy,
    AggregateFunction Aggregation,
    string? Field,
    IReadOnlyList<ChartBucket> Buckets);

/// <summary>One data point on the chart's category axis.</summary>
/// <param name="Label">String-friendly group key (e.g. <c>"Open"</c>, <c>"Paid"</c>, <c>"2026-04"</c>). Null group keys surface as <c>"(null)"</c>.</param>
/// <param name="Value">
/// Aggregate value for the bucket. <see langword="null"/> when the aggregation
/// is <c>Avg</c> / <c>Min</c> / <c>Max</c> over a group with no usable values
/// — the frontend renders "—" for that data point. <c>Count</c> and <c>Sum</c>
/// always carry a non-null value (zero for empty groups).
/// </param>
public sealed record ChartBucket(string Label, decimal? Value);
