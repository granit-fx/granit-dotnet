using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Execution;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for per-entity registration of import executors and identity resolvers.
/// </summary>
public static class DataExchangeEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Declares a <see cref="DbContext"/> whose entities participate in data exchange:
    /// auto-export definition discovery and fallback <c>IExportDataSource&lt;T&gt;</c> resolution.
    /// </summary>
    /// <remarks>
    /// Discovery is strictly registration-based — call this once per DbContext whose entities
    /// should be exportable without an explicit <c>ExportDefinition</c>/<c>IExportDataSource</c>.
    /// Idempotent: registering the same context twice is a no-op.
    /// </remarks>
    /// <typeparam name="TContext">The application DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataExchangeDbContext<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        bool alreadyRegistered = services.Any(d =>
            d.ServiceType == typeof(DataExchangeDbContextRegistration)
            && d.ImplementationInstance is DataExchangeDbContextRegistration registration
            && registration.ContextType == typeof(TContext));

        if (!alreadyRegistered)
        {
            services.AddSingleton(new DataExchangeDbContextRegistration(typeof(TContext)));
        }

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IImportExecutor{TEntity}"/> backed by EF Core.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TContext">The application DbContext type containing the entity.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImportExecutor<TEntity, TContext>(
        this IServiceCollection services)
        where TEntity : class
        where TContext : DbContext =>
        services.AddScoped<IImportExecutor<TEntity>, EfImportExecutor<TEntity, TContext>>();

    /// <summary>
    /// Registers a <see cref="BusinessKeyResolver{TEntity, TContext}"/> as the identity resolver.
    /// Uses a single business key property declared via <c>HasBusinessKey()</c> in the import definition.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TContext">The application DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBusinessKeyResolver<TEntity, TContext>(
        this IServiceCollection services)
        where TEntity : class
        where TContext : DbContext =>
        services.AddScoped<IRecordIdentityResolver<TEntity>, BusinessKeyResolver<TEntity, TContext>>();

    /// <summary>
    /// Registers a <see cref="CompositeKeyResolver{TEntity, TContext}"/> as the identity resolver.
    /// Uses multiple business key properties declared via <c>HasCompositeKey()</c> in the import definition.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TContext">The application DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCompositeKeyResolver<TEntity, TContext>(
        this IServiceCollection services)
        where TEntity : class
        where TContext : DbContext =>
        services.AddScoped<IRecordIdentityResolver<TEntity>, CompositeKeyResolver<TEntity, TContext>>();

    /// <summary>
    /// Registers an <see cref="ExternalIdResolver{TEntity, TContext}"/> as the identity resolver.
    /// Uses a dedicated external ID mapping table for roundtrip INSERT/UPDATE resolution.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TContext">The application DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExternalIdResolver<TEntity, TContext>(
        this IServiceCollection services)
        where TEntity : class
        where TContext : DbContext =>
        services.AddScoped<IRecordIdentityResolver<TEntity>, ExternalIdResolver<TEntity, TContext>>();
}
