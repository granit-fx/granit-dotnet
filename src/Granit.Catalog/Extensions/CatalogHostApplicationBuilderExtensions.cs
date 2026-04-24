using Granit.Catalog.Diagnostics;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// Registers metrics, the activity source, and (in subsequent commits) the
    /// query/export definitions. EF Core persistence is added separately via
    /// <c>AddGranitCatalogEntityFrameworkCore</c>; HTTP endpoints via <c>MapGranitCatalog</c>.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitCatalog(
        this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<CatalogMetrics>();
        GranitActivitySourceRegistry.Register(CatalogActivitySource.Name);

        // Query/Export definitions and reader/writer interfaces are registered in commits 4-6.
        return builder;
    }
}
