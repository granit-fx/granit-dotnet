namespace Granit.Dashboards;

/// <summary>
/// Base record for every widget shipped by a <see cref="DashboardDefinition"/>.
/// Concrete widget kinds add their kind-specific configuration via inheritance — see
/// <c>Granit.Dashboards.Abstractions</c> for presentation-only widgets, and per-domain
/// packages (e.g. <c>Granit.Analytics</c>, future <c>Granit.IoT.Dashboards</c>) for
/// data-bound widgets.
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
/// data source (metric / query / IoT topic) — see ADR-038 §6.
/// Presentation-only widgets ignore this field.
/// </param>
public abstract record WidgetDefinition(
    string Slug,
    int Position,
    WidgetSize Size,
    string? RequiredPermission = null);
