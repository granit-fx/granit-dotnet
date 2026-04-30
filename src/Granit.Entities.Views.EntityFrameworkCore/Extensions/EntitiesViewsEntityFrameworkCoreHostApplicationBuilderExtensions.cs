using Granit.Entities.Views.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Entities.Views.EntityFrameworkCore.Extensions;

/// <summary>
/// Host-side DI registration for the <see cref="IEntityViewReader"/> +
/// <see cref="IEntityViewWriter"/> stores backed by EF Core (per ADR-047).
/// </summary>
public static class EntitiesViewsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Wires the isolated <c>EntityViewDbContext</c> with the chosen EF Core provider
    /// and registers the <see cref="IEntityViewReader"/> / <see cref="IEntityViewWriter"/>
    /// implementations.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitEntitiesViews()</c>. The <c>EntityViewDbContext</c>
    /// is intentionally <c>internal</c> — the host configures the provider through
    /// <paramref name="configure"/> without ever referencing the type.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Provider configuration (e.g. <c>opts.UseNpgsql(connectionString)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitEntitiesViewsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<EntityViewDbContext>(configure);
        builder.Services.TryAddScoped<IEntityViewReader, EntityViewReader>();
        builder.Services.TryAddScoped<IEntityViewWriter, EntityViewWriter>();

        return builder;
    }
}
