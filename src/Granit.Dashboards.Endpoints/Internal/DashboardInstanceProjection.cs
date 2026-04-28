using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;

namespace Granit.Dashboards.Endpoints.Internal;

/// <summary>
/// Maps the persisted <see cref="Dashboard"/> aggregate (and its
/// <see cref="WidgetInstance"/> rows) to the wire DTOs returned by the list /
/// read-by-id endpoints. Extracted so the projection logic stays testable
/// without any HTTP harness.
/// </summary>
internal static class DashboardInstanceProjection
{
    public static DashboardSummaryResponse ToSummary(Dashboard dashboard)
        => new(
            Id: dashboard.Id,
            Name: dashboard.Name,
            Category: dashboard.Category,
            Status: dashboard.Status,
            IsSystem: dashboard.IsSystem,
            SourceDefinitionName: dashboard.SourceDefinitionName,
            SourceDefinitionVersion: dashboard.SourceDefinitionVersion,
            WidgetCount: dashboard.Widgets.Count);

    public static DashboardDetailResponse ToDetail(Dashboard dashboard)
        => new(
            Id: dashboard.Id,
            Name: dashboard.Name,
            Category: dashboard.Category,
            Status: dashboard.Status,
            IsSystem: dashboard.IsSystem,
            SourceDefinitionName: dashboard.SourceDefinitionName,
            SourceDefinitionVersion: dashboard.SourceDefinitionVersion,
            LayoutColumns: dashboard.LayoutColumns,
            LayoutRowHeight: dashboard.LayoutRowHeight,
            Widgets: [.. dashboard.Widgets.OrderBy(w => w.Position).Select(ToWidgetInstance)]);

    public static WidgetInstanceResponse ToWidgetInstance(WidgetInstance widget)
        => new(
            Id: widget.Id,
            WidgetType: widget.WidgetType,
            Position: widget.Position,
            Width: widget.Width,
            Height: widget.Height,
            TitleLocalizationKey: widget.TitleLocalizationKey,
            MetricName: widget.MetricName,
            QueryName: widget.QueryName,
            ConfigJson: widget.ConfigJson,
            RequiredPermission: widget.RequiredPermission);
}
