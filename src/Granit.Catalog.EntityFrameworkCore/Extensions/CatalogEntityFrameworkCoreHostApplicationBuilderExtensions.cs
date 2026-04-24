using Granit.Catalog.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        builder.Services.AddGranitDbContext<CatalogDbContext>(configure);

        builder.Services.TryAddScoped<IProductReader, EfProductReader>();
        builder.Services.TryAddScoped<IProductWriter, EfProductWriter>();

        return builder;
    }
}
