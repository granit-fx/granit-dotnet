using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.Database.Diagnostics;
using Granit.BlobStorage.Database.Internal;
using Granit.BlobStorage.Database.Options;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Diagnostics;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Database.Extensions;

/// <summary>
/// Extension methods for registering the database blob storage provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageDatabaseHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.Database</c> services: database client, key strategy, isolated
    /// <see cref="BlobStorageDatabaseDbContext"/>, and the <see cref="IBlobStorage"/> orchestrator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="DatabaseBlobOptions"/> from the <c>"BlobStorage"</c> configuration section.
    /// </para>
    /// <para>
    /// This provider does NOT register <see cref="IPresignedUrlProvider"/>.
    /// Use <c>AddGranitBlobStorageProxy()</c> to provide proxy-based pre-signed URLs.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configures the <see cref="DbContextOptionsBuilder"/> for the isolated database context (e.g. <c>UseNpgsql</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageDatabase(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        GranitActivitySourceRegistry.Register(BlobStorageDatabaseActivitySource.Name);

        builder.Services
            .AddOptions<DatabaseBlobOptions>()
            .BindConfiguration(BlobStorageOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<DatabaseBlobOptions>, DatabaseBlobOptionsValidator>();

        builder.Services.AddGranitDbContext<BlobStorageDatabaseDbContext>(configure);
        builder.Services.AddTenantInternalDbContextEnsurer<BlobStorageDatabaseDbContext>();

        builder.Services.AddScoped<IBlobStoreProvider, DatabaseBlobClient>();
        builder.Services.AddScoped<IBlobKeyStrategy, DatabaseBlobKeyStrategy>();
        builder.Services.AddScoped<IBlobStorage, DefaultBlobStorage>();

        return builder;
    }
}
