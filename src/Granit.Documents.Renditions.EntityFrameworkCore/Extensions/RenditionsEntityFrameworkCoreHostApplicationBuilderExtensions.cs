using System;
using Granit.Documents.Renditions.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Extensions;

/// <summary>Extensions for registering EF Core persistence for <c>Granit.Documents.Renditions</c>.</summary>
public static class RenditionsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the renditions <c>IRenditionStore</c> backed by <see cref="RenditionsDbContext"/>
    /// and wires the cascade handler on <c>DocumentPermanentlyDeletedEvent</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="RenditionsDbContext"/> via <c>AddGranitIsolatedDbContext</c>
    /// — renditions are tenant-scoped and inherit the active multi-tenancy isolation strategy.
    /// </para>
    /// <para>
    /// Must be called after <c>AddGranitDocumentsEntityFrameworkCore</c> so that
    /// <c>ITenantQuotaService</c> is registered — this module decrements the
    /// rendition counter through it on cascade.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy.</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDocumentsRenditionsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<RenditionsDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);
        builder.Services.TryAddScoped<IRenditionStore, RenditionStore>();
        builder.Services.TryAddScoped<IRenditionService, RenditionService>();

        return builder;
    }
}
