using Microsoft.Extensions.Hosting;

namespace Granit.Catalog.Extensions;

/// <summary>
/// Extension methods for registering the Granit catalog infrastructure.
/// </summary>
public static class CatalogHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit catalog infrastructure (Product aggregate, lifecycle, external mappings).
    /// </summary>
    /// <remarks>
    /// Registers the catalog domain services. EF Core persistence is added separately via
    /// <c>AddGranitCatalogEntityFrameworkCore</c>; HTTP endpoints via <c>MapGranitCatalog</c>.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitCatalog(
        this IHostApplicationBuilder builder)
    {
        // Domain services, metrics, query/export definitions, and activity source
        // registration will be added as the module is fleshed out (commits 2-6).
        return builder;
    }
}
