using Granit.Diagnostics;
using Granit.MultiTenancy;
using Granit.TextExtraction.Diagnostics;
using Granit.TextExtraction.Internal;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.TextExtraction.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.TextExtraction</c> runtime services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the text-extraction pipeline, the plain-text fallback extractor, options,
    /// metrics, and the activity source. Concrete extractors (PDF, Office, HTML, OCR, ...)
    /// are registered through their own packages or via <see cref="AddTextExtractor{T}"/>.
    /// </summary>
    public static IServiceCollection AddGranitTextExtraction(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(TextExtractionActivitySource.Name);

        services.AddOptions<GranitTextExtractionOptions>()
            .BindConfiguration(GranitTextExtractionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<TextExtractionMetrics>();
        services.TryAddSingleton<PlainTextExtractor>();
        services.TryAddSingleton<ICurrentTenant>(NullTenantContext.Instance);
        services.TryAddSingleton<ITextExtractionPipeline, TextExtractionPipeline>();

        return services;
    }

    /// <summary>
    /// Adds a concrete <see cref="ITextExtractor"/> implementation to the pipeline.
    /// Extractors are evaluated in registration order — register the most specific first.
    /// </summary>
    public static IServiceCollection AddTextExtractor<TExtractor>(this IServiceCollection services)
        where TExtractor : class, ITextExtractor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITextExtractor, TExtractor>();
        return services;
    }
}
