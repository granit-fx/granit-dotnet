using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.FileSystem.Diagnostics;
using Granit.BlobStorage.FileSystem.Internal;
using Granit.BlobStorage.FileSystem.Options;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.FileSystem.Extensions;

/// <summary>
/// Extension methods for registering the local file system blob storage provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageFileSystemHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.FileSystem</c> services: file system client, key strategy, and the
    /// <see cref="IBlobStorage"/> orchestrator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="FileSystemBlobOptions"/> from the <c>"BlobStorage"</c> configuration section.
    /// </para>
    /// <para>
    /// This provider does NOT register <see cref="IPresignedUrlProvider"/>.
    /// Use <c>AddGranitBlobStorageProxy()</c> to provide proxy-based pre-signed URLs.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageFileSystem(
        this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(BlobStorageFileSystemActivitySource.Name);

        builder.Services
            .AddOptions<FileSystemBlobOptions>()
            .BindConfiguration(BlobStorageOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<FileSystemBlobOptions>, FileSystemBlobOptionsValidator>();

        builder.Services.AddSingleton<IBlobStoreProvider, FileSystemBlobClient>();
        builder.Services.AddScoped<IBlobKeyStrategy, FileSystemBlobKeyStrategy>();
        builder.Services.AddScoped<IBlobStorage, DefaultBlobStorage>();

        return builder;
    }
}
