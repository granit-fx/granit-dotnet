namespace Granit.Catalog.Endpoints.Options;

/// <summary>
/// Configuration options for the catalog endpoints.
/// </summary>
public sealed class CatalogEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "CatalogEndpoints";

    /// <summary>
    /// Route prefix for all catalog endpoints.
    /// Default: <c>"catalog"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "catalog";

    /// <summary>
    /// OpenAPI tag name for product catalog endpoints.
    /// Default: <c>"Catalog - Products"</c>.
    /// </summary>
    public string ProductsTagName { get; set; } = "Catalog - Products";
}
