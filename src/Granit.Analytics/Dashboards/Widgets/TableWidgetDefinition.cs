using Granit.Dashboards;

namespace Granit.Analytics.Dashboards.Widgets;

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
/// <param name="TimeWindowOverride">See <see cref="WidgetDefinition.TimeWindowOverride"/>.</param>
public sealed record TableWidgetDefinition(
    string Slug,
    string QueryName,
    IReadOnlyList<string>? VisibleColumns,
    int PageSize,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null,
    DashboardTimeWindow? TimeWindowOverride = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.StandardChart, RequiredPermission, TimeWindowOverride);
