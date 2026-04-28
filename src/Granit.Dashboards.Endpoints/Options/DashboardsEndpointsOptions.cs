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
    /// OpenAPI tag for the dashboards endpoints. Per CLAUDE.md sub-tag convention
    /// (<c>&lt;Module&gt; - &lt;SubGroup&gt;</c>) the catalogue gets its own subgroup
    /// to keep room for the upcoming import / CRUD endpoints.
    /// </summary>
    public string CatalogTagName { get; set; } = "Dashboards - Catalogue";
}
