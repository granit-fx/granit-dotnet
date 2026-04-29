namespace Granit.Analytics.Rendering;

/// <summary>
/// Comparison-period side of a metric response.
/// </summary>
/// <param name="Value">The metric value over the comparison window.</param>
/// <param name="DeltaRatio">
/// Relative change between the current and the comparison value, in <c>[-1, +∞)</c>.
/// E.g. <c>0.15</c> means the current value is 15% above the previous one.
/// <c>null</c> when the previous value is zero (delta undefined) or null (no data).
/// </param>
/// <param name="Trend">Direction of the change: <c>up</c>, <c>down</c>, or <c>flat</c>.</param>
/// <param name="IsFavorable">
/// Whether the change is favorable — derived from <c>Trend</c> and the metric's
/// <c>IsHigherBetter</c> flag. Drives the green/red color of the delta arrow on the frontend.
/// </param>
public sealed record MetricPreviousPayload(
    decimal? Value,
    double? DeltaRatio,
    string Trend,
    bool? IsFavorable);
