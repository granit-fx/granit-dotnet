using System.Diagnostics.CodeAnalysis;
using Granit.AI.Extraction.Internal;
using Granit.AI.Extraction.Options;
using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.AI.Extraction.Extensions;

/// <summary>
/// Extension methods for registering Granit AI extraction services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AIExtractionServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit AI extraction core services and binds <see cref="ExtractionOptions"/>
    /// from the <c>AI:Extraction</c> configuration section.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAIExtraction(this IHostApplicationBuilder builder)
    {
        AddGranitAIExtractionCore(builder.Services);
        return builder;
    }

    /// <summary>
    /// Registers the cross-cutting services consumed by every AI-feature package
    /// (rate limiter, content-redactor seam, extraction options). Called both from
    /// <see cref="AddGranitAIExtraction(IHostApplicationBuilder)"/> and from
    /// <c>GranitAIExtractionModule.ConfigureServices</c> so module-scanned hosts and
    /// hand-wired hosts end up with the same defaults.
    /// </summary>
    internal static IServiceCollection AddGranitAIExtractionCore(IServiceCollection services)
    {
        services
            .AddOptions<ExtractionOptions>()
            .BindConfiguration(ExtractionOptions.SectionName);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAICallRateLimiter, AICallRateLimiter>();
        services.TryAddSingleton<IAIContentRedactor, NoOpAIContentRedactor>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="IDocumentExtractor{TResult}"/> for the specified result type
    /// using the default LLM-based implementation.
    /// </summary>
    /// <typeparam name="TResult">The type of the structured data to extract.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentExtractor<TResult>(this IServiceCollection services)
        where TResult : class
    {
        services.TryAddScoped<IDocumentExtractor<TResult>, DefaultDocumentExtractor<TResult>>();
        return services;
    }
}
