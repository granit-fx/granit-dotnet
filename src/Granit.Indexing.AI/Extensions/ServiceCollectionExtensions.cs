using Granit.AI.Extraction.Redaction;
using Granit.Diagnostics;
using Granit.Indexing.AI.Diagnostics;
using Granit.Indexing.AI.Internal;
using Granit.Indexing.AI.Options;
using Granit.Indexing.AI.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        RegisterCommon(services);
        services.TryAddSingleton<IAIAutoSummaryPromptBuilder, DefaultAIAutoSummaryPromptBuilder>();
        services.TryAddScoped<ISummarizer, AISummarizer>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="AIAutoTagger"/> as the host's <see cref="IAutoTagger"/>, the
    /// default prompt builder, options binding, and metrics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosts MUST also register an <see cref="ITagCandidateProvider"/> implementation
    /// (one per tenant tag universe) so the auto-tagger has a candidate list at runtime,
    /// AND an AI provider package (e.g. <c>Granit.AI.OpenAI</c>) so <c>IAIChatClientFactory</c>
    /// can resolve a client.
    /// </para>
    /// <para>
    /// <see cref="IAutoTagger"/> is registered via <c>TryAddScoped</c> — hosts substituting
    /// a custom auto-tagger register it BEFORE calling this extension.
    /// </para>
    /// <para>
    /// <b>⚠ Suggestion-only UX contract.</b> User-facing UI MUST require explicit
    /// confirmation before applying suggested tags. The 'suggestion-only' guarantee is a
    /// UX contract — in bulk-approve flows, this defence becomes ineffective.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitIndexingAIAutoTagger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        RegisterCommon(services);
        services.TryAddSingleton<IAutoTagPromptBuilder, DefaultAutoTagPromptBuilder>();
        services.TryAddScoped<IAutoTagger, AIAutoTagger>();

        return services;
    }

    private static void RegisterCommon(IServiceCollection services)
    {
        GranitActivitySourceRegistry.Register(IndexingAIMetrics.MeterName);

        // ValidateOnStart aborts host boot on an out-of-range option rather than
        // silently degrading every call (which would inflate the injection counters).
        services.AddOptions<IndexingAIOptions>()
            .BindConfiguration(IndexingAIOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IndexingAIMetrics>();

        // Warn at startup when RedactPIIBeforeLLMCall is enabled but only the identity
        // NoOpAIContentRedactor is registered. RegisterCommon runs once per wired feature
        // (summarizer / auto-tagger) — gate on a marker so the probe is added exactly once.
        if (!services.Any(d => d.ServiceType == typeof(RedactionWarningMarker)))
        {
            services.AddSingleton<RedactionWarningMarker>();
            services.AddAIRedactionStartupWarning(
                "Granit.Indexing.AI",
                sp => sp.GetRequiredService<IOptions<IndexingAIOptions>>().Value.RedactPIIBeforeLLMCall);
        }
    }

    private sealed class RedactionWarningMarker;
}
