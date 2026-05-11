using System;
using Granit.Documents.AssetMetadata.Extractors;
using Granit.Documents.AssetMetadata.Pdf.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.AssetMetadata.Pdf.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.AssetMetadata.Pdf</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PdfMetadataExtractor"/> as an
    /// <see cref="IAssetMetadataExtractor"/>. Safe to call multiple times —
    /// subsequent calls are no-ops via <c>TryAddEnumerable</c>.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsAssetMetadataPdf(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAssetMetadataExtractor, PdfMetadataExtractor>());
        return services;
    }
}
