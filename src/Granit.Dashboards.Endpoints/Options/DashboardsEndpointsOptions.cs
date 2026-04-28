namespace Granit.Dashboards.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Dashboards endpoint surface.
/// </summary>
public sealed class DashboardsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DashboardsEndpoints";

    /// <summary>
    /// Route prefix for dashboards endpoints. Default: <c>"dashboards"</c>
    /// → final route <c>/dashboards/catalog</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "dashboards";

    /// <summary>
    /// OpenAPI tag for the dashboard catalogue endpoints (read-only view of every
    /// registered <c>DashboardDefinition</c>). Per CLAUDE.md sub-tag convention
    /// (<c>&lt;Module&gt; - &lt;SubGroup&gt;</c>) the catalogue gets its own subgroup
    /// so Scalar groups it alphabetically next to its siblings.
    /// </summary>
    public string CatalogTagName { get; set; } = "Dashboards - Catalogue";

    /// <summary>
    /// OpenAPI tag for the persisted Dashboard aggregate endpoints — list,
    /// read-by-id, import, edit metadata, state transitions (publish / archive
    /// / restore). Per CLAUDE.md sub-tag convention.
    /// </summary>
    public string InstancesTagName { get; set; } = "Dashboards - Instances";

    /// <summary>
    /// OpenAPI tag for the widget pool endpoints — add / update / remove widget
    /// instances on a persisted dashboard. Per CLAUDE.md sub-tag convention.
    /// </summary>
    public string WidgetsTagName { get; set; } = "Dashboards - Widgets";
}
