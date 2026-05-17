using Granit.BlobStorage.Domain;
using Granit.BlobStorage.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.BlobStorage.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit blob storage.
/// </summary>
public static class BlobStorageEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit blob storage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="BlobStorageDbContext"/> via <c>AddGranitIsolatedDbContext</c>
    /// and binds <see cref="IBlobDescriptorStore"/> to <c>EfBlobDescriptorStore</c>.
    /// BlobStorage is tenant-only: descriptors are isolated per tenant through the active
    /// <c>TenantIsolationStrategy</c>.
    /// </para>
    /// <para>
    /// <see cref="Granit.Persistence.EntityFrameworkCore.Interceptors.AuditedEntityInterceptor"/> is added automatically when
    /// <c>Granit.Persistence</c> is configured, enabling the ISO 27001 3-year audit trail.
    /// </para>
    /// <para>
    /// Must be called after <c>AddGranitBlobStorageS3()</c> (or any other blob storage provider).
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy.</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<BlobStorageDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);

        builder.Services.TryAddScoped<EfBlobDescriptorStore>();
        builder.Services.TryAddScoped<IBlobDescriptorStore>(sp => sp.GetRequiredService<EfBlobDescriptorStore>());
        builder.Services.TryAddScoped<IBlobDescriptorReader>(sp => sp.GetRequiredService<EfBlobDescriptorStore>());
        builder.Services.TryAddScoped<IBlobDescriptorWriter>(sp => sp.GetRequiredService<EfBlobDescriptorStore>());

        builder.Services.TryAddScoped<IQueryableSource<BlobDescriptor>, EfBlobQueryableSource>();

        return builder;
    }
}
