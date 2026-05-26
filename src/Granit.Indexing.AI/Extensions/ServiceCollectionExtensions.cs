using Granit.Diagnostics;
using Granit.Indexing.AI.Diagnostics;
using Granit.Indexing.AI.Internal;
using Granit.Indexing.AI.Options;
using Granit.Indexing.AI.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Indexing.AI.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Indexing.AI</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AISummarizer"/> as the host's <see cref="ISummarizer"/>,
    /// the default prompt builder, options binding, and metrics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosts MUST also register an AI provider package (e.g. <c>Granit.AI.OpenAI</c>)
    /// so <c>IAIChatClientFactory</c> can resolve a client at runtime. The rate
    /// limiter and content-redactor seam are registered by
    /// <c>Granit.AI.Extraction.GranitAIExtractionModule</c>.
    /// </para>
    /// <para>
    /// <see cref="ISummarizer"/> is registered via <c>TryAddScoped</c> — hosts that
    /// want to substitute a custom summarizer register it BEFORE calling this extension.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitIndexingAISummarizer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(IndexingAIMetrics.MeterName);

        services.AddOptions<IndexingAIOptions>()
            .BindConfiguration(IndexingAIOptions.SectionName);

        services.TryAddSingleton<IndexingAIMetrics>();
        services.TryAddSingleton<IAIAutoSummaryPromptBuilder, DefaultAIAutoSummaryPromptBuilder>();
        services.TryAddScoped<ISummarizer, AISummarizer>();

        return services;
    }
}
