using System;
using Granit.Documents.AssetMetadata.Diagnostics;
using Granit.Documents.AssetMetadata.Extractors;
using Granit.Documents.AssetMetadata.Options;
using Granit.Documents.AssetMetadata.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.AssetMetadata.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.AssetMetadata</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the asset-metadata pipeline + options + diagnostics. Extractor
    /// packages (<c>Granit.Documents.AssetMetadata.Imaging</c>, <c>.Pdf</c>,
    /// <c>.Office</c>, <c>.Media</c>) and the storage companion plug in on top
    /// via their own extension methods.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsAssetMetadata(
        this IServiceCollection services,
        Action<GranitAssetMetadataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GranitAssetMetadataOptions>()
            .BindConfiguration(GranitAssetMetadataOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<AssetMetadataMetrics>();
        services.TryAddSingleton<IAssetMetadataPipeline, AssetMetadataPipeline>();

        return services;
    }

    /// <summary>
    /// Helper used by extractor packages to register an
    /// <see cref="IAssetMetadataExtractor"/> implementation.
    /// </summary>
    public static IServiceCollection AddAssetMetadataExtractor<TExtractor>(this IServiceCollection services)
        where TExtractor : class, IAssetMetadataExtractor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAssetMetadataExtractor, TExtractor>();
        return services;
    }
}
