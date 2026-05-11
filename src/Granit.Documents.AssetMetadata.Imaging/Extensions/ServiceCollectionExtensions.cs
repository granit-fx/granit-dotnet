using System;
using Granit.Documents.AssetMetadata.Extractors;
using Granit.Documents.AssetMetadata.Imaging.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.AssetMetadata.Imaging.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.AssetMetadata.Imaging</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ImageMetadataExtractor"/> as an
    /// <see cref="IAssetMetadataExtractor"/>. Safe to call multiple times —
    /// subsequent calls are no-ops via <c>TryAddEnumerable</c>.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsAssetMetadataImaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAssetMetadataExtractor, ImageMetadataExtractor>());
        return services;
    }
}
