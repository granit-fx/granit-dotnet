using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods to register API key EF Core services.
/// </summary>
public static class ApiKeysEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core API key store and <see cref="AuthenticationApiKeysDbContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="AuthenticationApiKeysDbContext"/> via <c>AddGranitIsolatedDbContext</c>
    /// so the active <c>TenantIsolationStrategy</c> (<c>SharedDatabase</c>,
    /// <c>SchemaPerTenant</c>, <c>DatabasePerTenant</c>) is honored end-to-end. API keys
    /// are a tenant-only concern — under <c>SchemaPerTenant</c> the
    /// <c>TenantSchemaConnectionInterceptor</c> is wired automatically so unqualified
    /// queries land in the tenant's schema instead of <c>public</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy (always required).</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    public static IServiceCollection AddGranitApiKeysEntityFrameworkCore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureShared);

        services.AddGranitIsolatedDbContext<AuthenticationApiKeysDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);

        services.TryAddScoped<IApiKeyStore, EfCoreApiKeyStore>();
        services.TryAddScoped<IApiKeyAdminStore, EfCoreApiKeyAdminStore>();

        // Queryable source backing MapGranitQuery<ApiKeyEntry> (tenant filter honoured as-is).
        services.TryAddScoped<IQueryableSource<ApiKeyEntry>, EfApiKeyEntryQueryableSource>();

        return services;
    }
}
