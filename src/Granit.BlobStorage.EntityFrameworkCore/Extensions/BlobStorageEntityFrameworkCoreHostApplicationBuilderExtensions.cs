using Granit.BlobStorage.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    /// Registers <see cref="BlobStorageDbContext"/> via
    /// <c>AddGranitDbContext</c> (<c>IDbContextFactory</c> with interceptor DI)
    /// and binds <see cref="IBlobDescriptorStore"/> to <c>EfBlobDescriptorStore</c>.
    /// <para>
    /// <see cref="AuditedEntityInterceptor"/> is added automatically when
    /// <c>Granit.Persistence</c> is configured, enabling the ISO 27001 3-year audit trail.
    /// </para>
    /// <para>
    /// Must be called after <c>AddGranitBlobStorageS3()</c> (or any other blob storage provider).
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<BlobStorageDbContext>(configure);
        builder.Services.AddTenantInternalDbContextEnsurer<BlobStorageDbContext>();

        builder.Services.AddScoped<EfBlobDescriptorStore>();
        builder.Services.AddScoped<IBlobDescriptorStore>(sp => sp.GetRequiredService<EfBlobDescriptorStore>());
        builder.Services.AddScoped<IBlobDescriptorReader>(sp => sp.GetRequiredService<EfBlobDescriptorStore>());
        builder.Services.AddScoped<IBlobDescriptorWriter>(sp => sp.GetRequiredService<EfBlobDescriptorStore>());

        builder.Services.AddScoped<IBlobQueryableProvider, EfBlobQueryableProvider>();

        return builder;
    }
}
