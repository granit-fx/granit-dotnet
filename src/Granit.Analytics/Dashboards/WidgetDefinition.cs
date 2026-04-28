using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Dashboards;

/// <summary>
/// Base record for every widget shipped by a <see cref="DashboardDefinition"/>.
/// Concrete widget kinds add their kind-specific configuration via inheritance.
/// </summary>
/// <param name="Slug">
/// Widget-local identifier (PascalCase, unique within the dashboard). Used to compose
/// localization keys (<c>Widget:{DashboardName}.{Slug}</c>) and as the stable
/// identifier under reorder operations.
/// </param>
/// <param name="Position">Dense-ranked grid order — 0-based, contiguous within the dashboard.</param>
/// <param name="Size">Width / height in grid cells.</param>
/// <param name="RequiredPermission">
/// Optional override of the permission gating this widget at render time. When
/// <c>null</c>, the runtime resolves the effective permission from the underlying
/// metric / query (ADR-038 §6).
/// </param>
public abstract record WidgetDefinition(
    string Slug,
    int Position,
    WidgetSize Size,
    string? RequiredPermission = null);

/// <summary>
/// A single-value KPI tile bound to a <c>MetricDefinition</c>. The most common widget
/// kind — typically renders the metric value, an optional period delta, and a
/// favorable / unfavorable color cue driven by <c>MetricDefinition.IsHigherBetter</c>.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="MetricName">The <c>MetricDefinition.Name</c> this KPI surfaces (e.g. <c>"Granit.Invoicing.UnpaidInvoiceCountMetric"</c>).</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.SmallKpi"/>.</param>
/// <param name="RequiredPermission">See <see cref="WidgetDefinition.RequiredPermission"/>.</param>
public sealed record KpiWidgetDefinition(
    string Slug,
    string MetricName,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.SmallKpi, RequiredPermission);

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
public sealed record ChartWidgetDefinition(
    string Slug,
    string QueryName,
    string GroupBy,
    AggregateFunction Aggregation,
    string? Field,
    ChartType ChartType,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.StandardChart, RequiredPermission);

/// <summary>
/// Tabular widget bound to a <c>QueryDefinition</c>. Renders a paginated grid of rows
/// using the query's filter / sort surface.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="QueryName">The <c>QueryDefinition.Name</c> backing this table.</param>
/// <param name="VisibleColumns">Subset of the query's columns to surface, in order; <c>null</c> renders all.</param>
/// <param name="PageSize">Default page size for the embedded grid.</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.StandardChart"/>.</param>
/// <param name="RequiredPermission">See <see cref="WidgetDefinition.RequiredPermission"/>.</param>
public sealed record TableWidgetDefinition(
    string Slug,
    string QueryName,
    IReadOnlyList<string>? VisibleColumns,
    int PageSize,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.StandardChart, RequiredPermission);

/// <summary>
/// Pivot-table widget — rows × columns × value cells, classic OLAP shape.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="QueryName">The <c>QueryDefinition.Name</c> backing this pivot.</param>
/// <param name="RowFields">Fields used as row dimensions (in order).</param>
/// <param name="ColumnFields">Fields used as column dimensions (in order).</param>
/// <param name="ValueField">Field aggregated in each cell; <c>null</c> when <see cref="ValueAggregation"/> is <see cref="AggregateFunction.Count"/>.</param>
/// <param name="ValueAggregation">Aggregation applied to <see cref="ValueField"/>.</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.StandardChart"/>.</param>
/// <param name="RequiredPermission">See <see cref="WidgetDefinition.RequiredPermission"/>.</param>
public sealed record PivotWidgetDefinition(
    string Slug,
    string QueryName,
    IReadOnlyList<string> RowFields,
    IReadOnlyList<string> ColumnFields,
    string? ValueField,
    AggregateFunction ValueAggregation,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.StandardChart, RequiredPermission);

/// <summary>
/// Static markdown content — does not query the data layer. Used for dashboard
/// banners, contextual notes, links to runbooks. Permission filtering does not apply
/// (markdown is always visible to anyone who can see the dashboard).
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="ContentLocalizationKey">Localization key resolving to the markdown body (e.g. <c>"Widget:Granit.Invoicing.FinanceOverview.Banner"</c>).</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.FullWidthRow"/>.</param>
public sealed record MarkdownWidgetDefinition(
    string Slug,
    string ContentLocalizationKey,
    int Position,
    WidgetSize? Size = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.FullWidthRow, RequiredPermission: null);

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
