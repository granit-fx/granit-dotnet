using Granit.Activities.EntityFrameworkCore.Internal;
using Granit.Activities.Persistence;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Activities.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Activities.
/// </summary>
public static class ActivitiesEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="ActivitiesDbContext"/> via <c>AddGranitIsolatedDbContext</c>.
    /// </summary>
    /// <remarks>
    /// Activities is a tenant-only module. The active <c>TenantIsolationStrategy</c>
    /// (<c>SharedDatabase</c>, <c>DatabasePerTenant</c>, <c>SchemaPerTenant</c>) is honored
    /// and the audit + soft-delete + lifecycle interceptors are wired automatically.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy.</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitActivitiesEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<ActivitiesDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);
        builder.Services.AddScoped<IActivityReader, EfCoreActivityReader>();
        builder.Services.AddScoped<IActivityWriter, EfCoreActivityWriter>();
        return builder;
    }
}
