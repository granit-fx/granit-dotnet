using Granit.Analytics.Extensions;
using Granit.Catalog.Diagnostics;
using Granit.Catalog.Domain;
using Granit.Catalog.Exports;
using Granit.Catalog.Metrics;
using Granit.Catalog.Queries;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine.Extensions;
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
    /// Registers metrics, the activity source, and the query/export definitions for
    /// admin grids and CSV/XLSX exports. EF Core persistence is added separately via
    /// <c>AddGranitCatalogEntityFrameworkCore</c>; HTTP endpoints via <c>MapGranitCatalog</c>.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitCatalog(
        this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<CatalogMetrics>();
        GranitActivitySourceRegistry.Register(CatalogActivitySource.Name);

        builder.Services.AddQueryDefinition<Product, ProductQueryDefinition>();
        builder.Services.AddExportDefinition<Product, ProductExportDefinition>();

        builder.Services.AddMetricDefinition<Product, int, ActiveProductCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Product, int, ProductCountMetricDefinition>();

        return builder;
    }
}
