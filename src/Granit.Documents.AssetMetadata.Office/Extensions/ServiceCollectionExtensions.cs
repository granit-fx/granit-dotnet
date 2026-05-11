using System;
using Granit.Documents.AssetMetadata.Extractors;
using Granit.Documents.AssetMetadata.Office.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.AssetMetadata.Office.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.AssetMetadata.Office</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OfficeMetadataExtractor"/> as an
    /// <see cref="IAssetMetadataExtractor"/>. Safe to call multiple times —
    /// subsequent calls are no-ops via <c>TryAddEnumerable</c>.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsAssetMetadataOffice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAssetMetadataExtractor, OfficeMetadataExtractor>());
        return services;
    }
}
