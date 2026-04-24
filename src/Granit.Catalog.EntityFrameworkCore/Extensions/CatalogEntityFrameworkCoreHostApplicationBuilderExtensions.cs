using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Granit.Catalog.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit catalog.
/// </summary>
public static class CatalogEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for the Granit catalog.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Delegate to configure the catalog DbContext options
    /// (provider, connection string, interceptors).</param>
    public static IHostApplicationBuilder AddGranitCatalogEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        // DbContextFactory wiring + reader/writer registration will be added in commit 4.
        return builder;
    }
}
