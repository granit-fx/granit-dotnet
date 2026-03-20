using Granit.Persistence.DataSeeding;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Granit.ReferenceData.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.ReferenceData.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core reference data stores in the DI container.
/// </summary>
public static class ReferenceDataEfCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IReferenceDataStoreReader{TEntity}"/> and <see cref="IReferenceDataStoreWriter{TEntity}"/>
    /// backed by an EF Core store using <typeparamref name="TDbContext"/> and a <see cref="IDataSeedContributor"/>
    /// bridge for <typeparamref name="TEntity"/> seeders.
    /// </summary>
    /// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
    /// <typeparam name="TDbContext">The host application's DbContext.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReferenceDataStore<TEntity, TDbContext>(
        this IServiceCollection services)
        where TEntity : ReferenceDataEntity
        where TDbContext : DbContext
    {
        services.AddScoped<EfCoreReferenceDataStore<TEntity, TDbContext>>(sp =>
            new EfCoreReferenceDataStore<TEntity, TDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IFusionCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReferenceDataOptions>>()));
        services.AddScoped<IReferenceDataStoreReader<TEntity>>(sp =>
            sp.GetRequiredService<EfCoreReferenceDataStore<TEntity, TDbContext>>());
        services.AddScoped<IReferenceDataStoreWriter<TEntity>>(sp =>
            sp.GetRequiredService<EfCoreReferenceDataStore<TEntity, TDbContext>>());

        services.AddTransient<IDataSeedContributor, ReferenceDataSeedContributor<TEntity>>();

        return services;
    }
}
