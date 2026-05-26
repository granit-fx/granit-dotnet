using Granit.Diagnostics;
using Granit.LanguageDetection.AI.Diagnostics;
using Granit.LanguageDetection.AI.Internal;
using Granit.LanguageDetection.AI.Options;
using Granit.LanguageDetection.AI.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.LanguageDetection.AI.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.LanguageDetection.AI</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AILanguageDetector"/> as an additional
    /// <see cref="ILanguageDetectorProvider"/> (priority <c>200</c>, runs before the
    /// pure-managed Trigram detector at priority <c>100</c>), plus the default
    /// prompt builder and metrics.
    /// </summary>
    /// <remarks>
    /// Hosts MUST also register an AI provider package (e.g. <c>Granit.AI.OpenAI</c>)
    /// so <c>IAIChatClientFactory</c> can resolve a client at runtime. The rate
    /// limiter and content-redactor seam are registered by
    /// <c>Granit.AI.Extraction.GranitAIExtractionModule</c> and consumed transparently
    /// by the detector.
    /// </remarks>
    public static IServiceCollection AddGranitLanguageDetectionAI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(LanguageDetectionAIMetrics.MeterName);

        services.AddOptions<LanguageDetectionAIOptions>()
            .BindConfiguration(LanguageDetectionAIOptions.SectionName);

        services.TryAddSingleton<LanguageDetectionAIMetrics>();
        services.TryAddSingleton<IAILanguageDetectionPromptBuilder, DefaultAILanguageDetectionPromptBuilder>();

        // Plug AILanguageDetector into the composite chain as an additional provider.
        // The composite resolves IEnumerable<ILanguageDetectorProvider>; using
        // TryAddEnumerable keeps the registration idempotent for re-entrant DI builds.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILanguageDetectorProvider, AILanguageDetector>());

        return services;
    }
}
