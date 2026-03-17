using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.DbStore.Diagnostics;
using Granit.BlobStorage.DbStore.Internal;
using Granit.BlobStorage.DbStore.Options;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Core.Diagnostics;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.DbStore.Extensions;

/// <summary>
/// Extension methods for registering the database blob storage provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageDbStoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.DbStore</c> services: database client, key strategy, isolated
    /// <see cref="DbStoreBlobStorageDbContext"/>, and the <see cref="IBlobStorage"/> orchestrator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="DbStoreBlobOptions"/> from the <c>"BlobStorage"</c> configuration section.
    /// </para>
    /// <para>
    /// This provider does NOT register <see cref="IPresignedUrlProvider"/>.
    /// Use <c>AddGranitBlobStorageProxy()</c> to provide proxy-based pre-signed URLs.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configures the <see cref="DbContextOptionsBuilder"/> for the isolated database context (e.g. <c>UseNpgsql</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageDbStore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        GranitActivitySourceRegistry.Register(BlobStorageDbStoreActivitySource.Name);

        builder.Services
            .AddOptions<DbStoreBlobOptions>()
            .BindConfiguration(BlobStorageOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<DbStoreBlobOptions>, DbStoreBlobOptionsValidator>();

        builder.Services.AddGranitDbContext<DbStoreBlobStorageDbContext>(configure);

        builder.Services.AddScoped<IBlobStoreProvider, DbStoreBlobClient>();
        builder.Services.AddScoped<IBlobKeyStrategy, DbStoreBlobKeyStrategy>();
        builder.Services.AddScoped<IBlobStorage, DefaultBlobStorage>();

        return builder;
    }
}
