using System;
using Granit.Documents.AssetMetadata.AudioVideo.Internal;
using Granit.Documents.AssetMetadata.Extractors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.AssetMetadata.AudioVideo.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.AssetMetadata.AudioVideo</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AudioVideoMetadataExtractor"/> as an
    /// <see cref="IAssetMetadataExtractor"/>. Safe to call multiple times —
    /// subsequent calls are no-ops via <c>TryAddEnumerable</c>.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsAssetMetadataAudioVideo(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAssetMetadataExtractor, AudioVideoMetadataExtractor>());
        return services;
    }
}
