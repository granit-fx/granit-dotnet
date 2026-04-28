using Granit.Dashboards;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Dashboards.Widgets;

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
/// <param name="TimeWindowOverride">See <see cref="WidgetDefinition.TimeWindowOverride"/>.</param>
public sealed record PivotWidgetDefinition(
    string Slug,
    string QueryName,
    IReadOnlyList<string> RowFields,
    IReadOnlyList<string> ColumnFields,
    string? ValueField,
    AggregateFunction ValueAggregation,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null,
    DashboardTimeWindow? TimeWindowOverride = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.StandardChart, RequiredPermission, TimeWindowOverride);
