using System.Text.Json.Serialization;
using Granit.QueryEngine.Filtering;

namespace Granit.Dashboards;

/// <summary>
/// Abstract data binding for a widget — decouples a widget from how its data
/// arrives. P2.2 of the dashboards-architecture-proposals roadmap. The same
/// "Daily Active Users" KPI tile can be backed by a metric (<see cref="MetricDatasource"/>)
/// on the analytics dashboard, by a query aggregate
/// (<see cref="QueryAggregateDatasource"/>) on an ad-hoc admin page, or by a
/// telemetry stream (<see cref="TelemetryDatasource"/>) on an IoT dashboard —
/// same widget shape, different source.
/// </summary>
/// <remarks>
/// JSON polymorphism uses the <c>"kind"</c> discriminator with kebab tags
/// (<c>metric</c>, <c>query-aggregate</c>, <c>iot-telemetry</c>). Stable wire
/// format — same approach as <see cref="WidgetDefinition"/>'s <c>"type"</c>
/// discriminator (P1.1) and <see cref="EntityAliasResolver"/>'s <c>"kind"</c>
/// (P2.3). Static factories (<see cref="Metric(string)"/>, <see cref="QueryAggregate"/>,
/// <see cref="Telemetry"/>) keep call sites compact.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(MetricDatasource), "metric")]
[JsonDerivedType(typeof(QueryAggregateDatasource), "query-aggregate")]
[JsonDerivedType(typeof(TelemetryDatasource), "iot-telemetry")]
public abstract record Datasource
{
    /// <summary>Convenience factory for <see cref="MetricDatasource"/>.</summary>
    public static MetricDatasource Metric(string metricName) => new(metricName);

    /// <summary>Convenience factory for <see cref="QueryAggregateDatasource"/>.</summary>
    public static QueryAggregateDatasource QueryAggregate(
        string queryName,
        AggregateFunction aggregation,
        string? field = null,
        IReadOnlyList<DataKeyFormat>? keyFormats = null)
        => new(queryName, aggregation, field, keyFormats);

    /// <summary>Convenience factory for <see cref="TelemetryDatasource"/>.</summary>
    public static TelemetryDatasource Telemetry(
        string entityAlias,
        string telemetryKey,
        TelemetryAggregation aggregation = TelemetryAggregation.Last,
        IReadOnlyList<DataKeyFormat>? keyFormats = null)
        => new(entityAlias, telemetryKey, aggregation, keyFormats);
}

/// <summary>
/// Bound to a registered <c>MetricDefinition</c> by name. The metric's own
/// aggregation, base filter and period selector apply — no extra knobs.
/// </summary>
/// <param name="MetricName">Wire identifier of the metric (e.g. <c>"Granit.Invoicing.UnpaidInvoiceCountMetric"</c>).</param>
public sealed record MetricDatasource(string MetricName) : Datasource;

/// <summary>
/// Bound to a registered <c>QueryDefinition</c> by name; applies an aggregation
/// over a chosen field (<c>null</c> field = <see cref="AggregateFunction.Count"/>).
/// </summary>
/// <param name="QueryName">Wire identifier of the query (e.g. <c>"Granit.Invoicing.InvoiceQuery"</c>).</param>
/// <param name="Aggregation">Aggregation function applied to <paramref name="Field"/>.</param>
/// <param name="Field">Field aggregated; <c>null</c> when aggregation is Count.</param>
/// <param name="KeyFormats">
/// Per-series presentation hints. When the consuming widget renders multiple
/// series (e.g. a chart with a group-by), each entry's <see cref="DataKeyFormat.Key"/>
/// matches a series identifier. <c>null</c> = use the active theme palette.
/// </param>
public sealed record QueryAggregateDatasource(
    string QueryName,
    AggregateFunction Aggregation,
    string? Field = null,
    IReadOnlyList<DataKeyFormat>? KeyFormats = null) : Datasource;

/// <summary>
/// Bound to live telemetry pushed from the runtime (IoT). Resolves the entity
/// at render time via the dashboard's <see cref="EntityAlias"/> bindings.
/// </summary>
/// <param name="EntityAlias">Entity alias name (see story P2.3) — resolves to a device id at render time.</param>
/// <param name="TelemetryKey">Telemetry key (e.g. <c>"temperature"</c>, <c>"pressure"</c>, <c>"rpm"</c>).</param>
/// <param name="Aggregation">
/// Aggregation applied over the dashboard's <c>TimeWindow</c>.
/// Defaults to <see cref="TelemetryAggregation.Last"/> — the most-recent reading.
/// </param>
/// <param name="KeyFormats">Per-series presentation hints (see <see cref="QueryAggregateDatasource.KeyFormats"/>).</param>
public sealed record TelemetryDatasource(
    string EntityAlias,
    string TelemetryKey,
    TelemetryAggregation Aggregation = TelemetryAggregation.Last,
    IReadOnlyList<DataKeyFormat>? KeyFormats = null) : Datasource;

/// <summary>Aggregation kinds for live telemetry streams.</summary>
public enum TelemetryAggregation
{
    /// <summary>Most-recent reading. The default — typical for instantaneous gauges.</summary>
    Last = 0,

    /// <summary>Arithmetic mean over the dashboard's time window.</summary>
    Avg = 1,

    /// <summary>Sum of values in the window — useful for cumulative measurements.</summary>
    Sum = 2,

    /// <summary>Lowest value in the window.</summary>
    Min = 3,

    /// <summary>Highest value in the window.</summary>
    Max = 4,

    /// <summary>Number of readings in the window — useful for rate-of-events checks.</summary>
    Count = 5,
}
