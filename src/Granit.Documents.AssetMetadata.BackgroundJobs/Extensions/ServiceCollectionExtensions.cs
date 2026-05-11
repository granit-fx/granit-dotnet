using System;
using Granit.Documents.AssetMetadata.BackgroundJobs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.AssetMetadata.BackgroundJobs.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.AssetMetadata.BackgroundJobs</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the F17.4 event-driven extraction flow: the orchestration
    /// service plus the default HTTP-based source fetcher. Wolverine discovers
    /// <see cref="Handlers.DocumentVersionAddedAssetMetadataHandler"/> from the
    /// assembly. Consumers must additionally call
    /// <c>AddGranitDocumentsAssetMetadata()</c> (base pipeline + store contracts)
    /// and an EF Core persistence registration (typically
    /// <c>AddGranitDocumentsAssetMetadataEntityFrameworkCore</c>).
    /// </summary>
    public static IServiceCollection AddGranitDocumentsAssetMetadataBackgroundJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(HttpAssetMetadataSourceFetcher.HttpClientName);

        services.TryAddSingleton<IAssetMetadataSourceFetcher, HttpAssetMetadataSourceFetcher>();
        services.TryAddSingleton<IAssetMetadataGenerationService, AssetMetadataGenerationService>();

        return services;
    }
}
