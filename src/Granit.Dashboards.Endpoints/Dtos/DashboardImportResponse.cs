namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Response payload for <c>POST /dashboards/from-definition/{name}</c>. Echoes the
/// freshly-imported <c>Dashboard</c> aggregate's identifying fields so the client
/// can navigate to the new persisted dashboard immediately.
/// </summary>
/// <param name="Id">Persisted dashboard identifier (UUID).</param>
/// <param name="Name">Dashboard name — copied from the source definition.</param>
/// <param name="Category">Coarse grouping — copied from the source definition.</param>
/// <param name="Status">Initial status. Always <see cref="DashboardStatus.Draft"/> for freshly imported dashboards.</param>
/// <param name="SourceDefinitionName">Wire identifier of the definition this dashboard was imported from.</param>
/// <param name="SourceDefinitionVersion">Semver of the definition at import time — used by the drift-detection UI.</param>
/// <param name="WidgetCount">Number of widgets pinned by the import (entry view for multi-view dashboards).</param>
public sealed record DashboardImportResponse(
    Guid Id,
    string Name,
    DashboardCategory Category,
    Granit.Dashboards.Domain.DashboardStatus Status,
    string SourceDefinitionName,
    string SourceDefinitionVersion,
    int WidgetCount);
