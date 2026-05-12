using System;
using Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Extensions;

/// <summary>Extensions for registering EF Core persistence for <c>Granit.Documents.AssetMetadata</c>.</summary>
public static class AssetMetadataEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="IAssetMetadataStore"/> backed by
    /// <see cref="AssetMetadataDbContext"/> and the cascade handler on
    /// <c>DocumentPermanentlyDeletedEvent</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="AssetMetadataDbContext"/> via <c>AddGranitIsolatedDbContext</c>
    /// — AssetMetadata is a tenant-only satellite of Documents and inherits the same
    /// per-tenant isolation strategy.
    /// </para>
    /// <para>
    /// Should be called after <c>AddGranitDocumentsEntityFrameworkCore</c> so the
    /// parent documents <c>DbContext</c> and events are already wired.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy.</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDocumentsAssetMetadataEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<AssetMetadataDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);
        builder.Services.TryAddScoped<IAssetMetadataStore, AssetMetadataStore>();
        builder.Services.TryAddScoped<IAssetMetadataService, AssetMetadataService>();

        return builder;
    }
}
