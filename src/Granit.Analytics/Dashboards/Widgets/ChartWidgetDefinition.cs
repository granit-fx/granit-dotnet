using Granit.Dashboards;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Dashboards.Widgets;

/// <summary>
/// Aggregated chart bound to a <c>QueryDefinition</c>. Unlike a KPI, a chart owns its
/// aggregation (sum / average / count over a chosen field) so the same query can
/// power multiple distinct charts.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="QueryName">The <c>QueryDefinition.Name</c> backing this chart (e.g. <c>"Granit.Invoicing.InvoiceQuery"</c>).</param>
/// <param name="GroupBy">Field used as the chart's category axis (e.g. <c>"IssuedAtMonth"</c>).</param>
/// <param name="Aggregation">Reuses <see cref="AggregateFunction"/> for parity with metrics.</param>
/// <param name="Field">Field aggregated; <c>null</c> when <see cref="Aggregation"/> is <see cref="AggregateFunction.Count"/>.</param>
/// <param name="ChartType">Visual representation hint — interpreted by the frontend.</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.StandardChart"/>.</param>
/// <param name="RequiredPermission">See <see cref="WidgetDefinition.RequiredPermission"/>.</param>
/// <param name="TimeWindowOverride">See <see cref="WidgetDefinition.TimeWindowOverride"/>.</param>
public sealed record ChartWidgetDefinition(
    string Slug,
    string QueryName,
    string GroupBy,
    AggregateFunction Aggregation,
    string? Field,
    ChartType ChartType,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null,
    DashboardTimeWindow? TimeWindowOverride = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.StandardChart, RequiredPermission, TimeWindowOverride);

/// <summary>Visual hint for chart widgets, interpreted by the frontend.</summary>
public enum ChartType
{
    /// <summary>Vertical bars; one bar per category.</summary>
    Bar = 0,

    /// <summary>Horizontal bars; one bar per category.</summary>
    HorizontalBar = 1,

    /// <summary>Line chart over an ordered category axis (typically time).</summary>
    Line = 2,

    /// <summary>Area chart — line chart with the area under it filled.</summary>
    Area = 3,

    /// <summary>Pie chart — proportional slices of a total.</summary>
    Pie = 4,

    /// <summary>Donut chart — pie chart with a hollow centre.</summary>
    Donut = 5,
}
