using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.ExtraProperties;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Granit.ReferenceData.Internal;
using Granit.ReferenceData.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <param name="scope">
    /// The multi-tenancy scope. Defaults to <see cref="ReferenceDataScope.Global"/>
    /// (shared across all tenants, managed by host admin).
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReferenceDataStore<TEntity, TDbContext>(
        this IServiceCollection services,
        ReferenceDataScope scope = ReferenceDataScope.Global)
        where TEntity : ReferenceDataEntity
        where TDbContext : DbContext
    {
        services.AddScoped<EfCoreReferenceDataStore<TEntity, TDbContext>>(sp =>
            new EfCoreReferenceDataStore<TEntity, TDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IFusionCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReferenceDataOptions>>(),
                scope,
                sp.GetRequiredService<ICurrentTenant>()));
        services.AddScoped<IReferenceDataStoreReader<TEntity>>(sp =>
            sp.GetRequiredService<EfCoreReferenceDataStore<TEntity, TDbContext>>());
        services.AddScoped<IReferenceDataStoreWriter<TEntity>>(sp =>
            sp.GetRequiredService<EfCoreReferenceDataStore<TEntity, TDbContext>>());

#pragma warning disable CS0618 // IDataSeedContributor: ReferenceData supports both host/tenant, migrating in a follow-up
        services.AddTransient<IDataSeedContributor, ReferenceDataSeedContributor<TEntity>>();
#pragma warning restore CS0618

        return services;
    }

    /// <summary>
    /// Registers one or more dynamic reference data types backed by <typeparamref name="TDbContext"/>
    /// using a fluent builder API. Each type gets its own keyed store services and registry entry.
    /// </summary>
    /// <typeparam name="TDbContext">The host application's DbContext.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Fluent configuration delegate for declaring reference data types.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Each type declared via <c>rd.Add("Countries", ...)</c> registers:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Keyed <see cref="IReferenceDataStoreReader{TEntity}"/> and
    /// <see cref="IReferenceDataStoreWriter{TEntity}"/> (key = type name)
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// An <see cref="ExtraPropertySyncInterceptor"/> for Shadow Property synchronization
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// A <see cref="ReferenceDataTypeRegistration"/> in the singleton <see cref="ReferenceDataRegistry"/>
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// The existing generic API (<c>AddReferenceDataStore&lt;TEntity, TDb&gt;()</c>) remains
    /// unchanged for strongly-typed subclass usage.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddReferenceData&lt;AppDbContext&gt;(rd =&gt;
    /// {
    ///     rd.Add("Countries", opts =&gt; opts
    ///         .Table("ref_countries")
    ///         .MapProperty&lt;string&gt;("Alpha3Code", maxLength: 3, isFilterable: true));
    ///
    ///     rd.Add("DocumentTypes", opts =&gt; opts.Table("ref_document_types"));
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddReferenceData<TDbContext>(
        this IServiceCollection services,
        Action<ReferenceDataBuilder> configure)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Build the registrations
        ReferenceDataBuilder builder = new();
        configure(builder);

        // Register the generic ExtraProperty infrastructure
        services.AddExtraPropertyInfrastructure();

        foreach (ReferenceDataTypeRegistration registration in builder.Registrations)
        {
            // 1. Register keyed store services (type name = key)
            // Uses DynamicReferenceDataEntity (concrete) since ReferenceDataEntity is abstract
            ReferenceDataScope registrationScope = registration.Scope;

            services.AddKeyedScoped<IReferenceDataStoreReader<DynamicReferenceDataEntity>>(
                registration.TypeName,
                (sp, _) => new EfCoreReferenceDataStore<DynamicReferenceDataEntity, TDbContext>(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IFusionCache>(),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReferenceDataOptions>>(),
                    registrationScope,
                    sp.GetRequiredService<ICurrentTenant>()));

            services.AddKeyedScoped<IReferenceDataStoreWriter<DynamicReferenceDataEntity>>(
                registration.TypeName,
                (sp, _) => new EfCoreReferenceDataStore<DynamicReferenceDataEntity, TDbContext>(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IFusionCache>(),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReferenceDataOptions>>(),
                    registrationScope,
                    sp.GetRequiredService<ICurrentTenant>()));

            // 2. Register ExtraProperty mappings for shadow columns
            if (registration.Options.PropertyMappings.Count > 0)
            {
                services.AddExtraPropertyMappings<ReferenceDataEntity>(opts =>
                {
                    foreach (ReferenceDataPropertyMapping mapping in registration.Options.PropertyMappings)
                    {
                        opts.Mappings.Add(new ExtraPropertyMapping(
                            mapping.Name, mapping.ClrType, mapping.MaxLength,
                            mapping.IsRequired, mapping.IsFilterable, mapping.IsSortable));
                    }
                });
            }

            // 3. Register QueryDefinition + IQueryEngine (keyed by type name)
            services.AddKeyedSingleton<QueryDefinition<DynamicReferenceDataEntity>>(
                registration.TypeName,
                (_, _) => new ReferenceDataQueryDefinition(registration.TypeName, registration.Options));

            // 4. Register in the singleton registry (deferred to hosted service start)
            services.AddSingleton<IReferenceDataRegistryContributor>(
                new ReferenceDataRegistryContributor(registration));
        }

        return services;
    }

}
