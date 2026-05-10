using System;
using Granit.Documents.Renditions.Diagnostics;
using Granit.Documents.Renditions.Options;
using Granit.Documents.Renditions.Pipeline;
using Granit.Documents.Renditions.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.Renditions.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.Renditions</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the rendition pipeline + options + diagnostics. Provider implementations
    /// (<c>AddGranitDocumentsRenditionsImaging</c>, <c>AddGranitDocumentsRenditionsPdf</c>,
    /// <c>AddGranitDocumentsRenditionsOffice</c>) and the storage companion
    /// (<c>AddGranitDocumentsRenditionsEntityFrameworkCore</c>) plug in on top via their
    /// own extension methods.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsRenditions(
        this IServiceCollection services,
        Action<GranitRenditionsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GranitRenditionsOptions>()
            .BindConfiguration(GranitRenditionsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<RenditionsMetrics>();
        services.TryAddSingleton<IRenditionPipeline, RenditionPipeline>();

        return services;
    }

    /// <summary>
    /// Helper used by provider packages to register an <see cref="IRenditionProvider"/>
    /// implementation. Singletons are appended to the collection so the pipeline solver
    /// can enumerate every registered provider.
    /// </summary>
    public static IServiceCollection AddRenditionProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IRenditionProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IRenditionProvider, TProvider>();
        return services;
    }
}
