using Granit.Analytics.Metrics;

namespace Granit.Analytics.Endpoints.Dtos;

/// <summary>
/// Snapshot side of a metric response — the value at a point in time, plus everything
/// the frontend needs to render and color a delta.
/// </summary>
/// <param name="Value">
/// The aggregated value, projected to <see cref="decimal"/> for wire transport (int / long /
/// decimal round-trip exactly; double values are within decimal precision for typical KPIs).
/// <c>null</c> when <paramref name="NoData"/> is <c>true</c>.
/// </param>
/// <param name="ValueKind">Semantic kind for frontend formatting (count, currency, percentage…).</param>
/// <param name="Currency">ISO 4217 code when <paramref name="ValueKind"/> is <see cref="MetricValueKind.Currency"/>.</param>
/// <param name="IsHigherBetter">Whether higher values are favorable — drives delta color.</param>
/// <param name="NoData">
/// <c>true</c> when an Avg / Min / Max returned no rows. The frontend renders <c>—</c> and
/// hides the delta block. <c>false</c> for non-empty results and for Count/Sum (which return
/// 0 over an empty set).
/// </param>
/// <param name="Previous">
/// Comparison-window data when the request included <c>compareTo</c>. <c>null</c> otherwise.
/// </param>
public sealed record MetricSnapshotPayload(
    decimal? Value,
    MetricValueKind ValueKind,
    string? Currency,
    bool IsHigherBetter,
    bool NoData,
    MetricPreviousPayload? Previous);
