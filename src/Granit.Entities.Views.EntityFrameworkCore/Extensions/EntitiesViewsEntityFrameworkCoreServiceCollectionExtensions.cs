using Granit.Entities.Views.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.Views.EntityFrameworkCore.Extensions;

/// <summary>
/// Host-side DI registration for the EntityView persistence layer — the
/// <see cref="IEntityViewReader"/> + <see cref="IEntityViewWriter"/> implementations.
/// </summary>
/// <remarks>
/// The host wires its own <see cref="EntityViewDbContext"/> via the standard
/// EF Core DbContextFactory registration with the chosen provider
/// (PostgreSQL / SQL Server / SQLite) — this module does not bind a provider so that
/// consumers stay portable.
/// </remarks>
public static class EntitiesViewsEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>Registers the EntityView reader / writer implementations.</summary>
    public static IServiceCollection AddGranitEntitiesViewsEntityFrameworkCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IEntityViewReader, EntityViewReader>();
        services.TryAddScoped<IEntityViewWriter, EntityViewWriter>();
        return services;
    }
}
