using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.AzureBlob.Diagnostics;
using Granit.BlobStorage.AzureBlob.Internal;
using Granit.BlobStorage.AzureBlob.Options;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AzureBlob.Extensions;

/// <summary>
/// Extension methods for registering the Azure Blob Storage provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageAzureBlobHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.AzureBlob</c> services: Azure client, key strategy, and the
    /// <see cref="IBlobStorage"/> orchestrator.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="AzureBlobOptions"/> from the <c>"BlobStorage"</c> configuration section.
    /// Credentials (<c>ConnectionString</c>) must be injected from Granit.Vault;
    /// never store them in <c>appsettings.json</c>.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageAzureBlob(
        this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(BlobStorageAzureActivitySource.Name);

        builder.Services
            .AddOptions<AzureBlobOptions>()
            .BindConfiguration(BlobStorageOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<AzureBlobOptions>, AzureBlobOptionsValidator>();

        // AzureBlobClient implements IBlobStoreProvider + IPresignedUrlProvider.
        // Registered as Singleton: BlobServiceClient is thread-safe and intended for reuse.
        builder.Services.TryAddSingleton<AzureBlobClient>();
        builder.Services.TryAddSingleton<IBlobStoreProvider>(sp => sp.GetRequiredService<AzureBlobClient>());
        builder.Services.TryAddSingleton<IPresignedUrlProvider>(sp => sp.GetRequiredService<AzureBlobClient>());

        builder.Services.TryAddScoped<IBlobKeyStrategy, AzureBlobKeyStrategy>();
        builder.Services.TryAddScoped<IBlobStorage, DefaultBlobStorage>();

        return builder;
    }
}
