using Granit.Dashboards.Domain;

namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Lightweight projection of a persisted <see cref="Dashboard"/> for the list
/// endpoint. Strips the widget tree — the read-by-id endpoint
/// (<see cref="DashboardDetailResponse"/>) carries the full payload.
/// </summary>
/// <param name="Id">Persisted dashboard identifier.</param>
/// <param name="Name">Tenant-renamable display name.</param>
/// <param name="Category">Coarse grouping.</param>
/// <param name="Status">Lifecycle state (Draft / Published / Archived).</param>
/// <param name="IsSystem">When true, only re-syncable; never deletable by tenant admins.</param>
/// <param name="SourceDefinitionName">Wire identifier of the source DashboardDefinition. Null for ad-hoc dashboards composed entirely in the UI.</param>
/// <param name="SourceDefinitionVersion">Version of the source definition at import time. Null for ad-hoc dashboards.</param>
/// <param name="WidgetCount">Number of widgets currently pinned on the dashboard.</param>
public sealed record DashboardSummaryResponse(
    Guid Id,
    string Name,
    DashboardCategory Category,
    DashboardStatus Status,
    bool IsSystem,
    string? SourceDefinitionName,
    string? SourceDefinitionVersion,
    int WidgetCount);
