using Granit.Dashboards.Domain;

namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Full payload for <c>GET /dashboards/{id}</c> — summary fields plus the widget
/// tree and layout values needed to render the dashboard.
/// </summary>
/// <param name="Id">Persisted dashboard identifier.</param>
/// <param name="Name">Tenant-renamable display name.</param>
/// <param name="Category">Coarse grouping.</param>
/// <param name="Status">Lifecycle state.</param>
/// <param name="IsSystem">When true, only re-syncable; never deletable by tenant admins.</param>
/// <param name="SourceDefinitionName">Source DashboardDefinition wire identifier. Null for ad-hoc dashboards.</param>
/// <param name="SourceDefinitionVersion">Source definition version at import time.</param>
/// <param name="LayoutColumns">Grid columns at the base viewport.</param>
/// <param name="LayoutRowHeight">Grid row height in CSS pixels at the base viewport.</param>
/// <param name="Widgets">Widgets pinned on the dashboard, in declared <c>Position</c> order.</param>
public sealed record DashboardDetailResponse(
    Guid Id,
    string Name,
    DashboardCategory Category,
    DashboardStatus Status,
    bool IsSystem,
    string? SourceDefinitionName,
    string? SourceDefinitionVersion,
    int LayoutColumns,
    int LayoutRowHeight,
    IReadOnlyList<WidgetInstanceResponse> Widgets);
