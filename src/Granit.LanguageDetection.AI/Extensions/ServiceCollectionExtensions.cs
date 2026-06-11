using Granit.AI.Redaction;
using Granit.LanguageDetection.AI.Diagnostics;
using Granit.LanguageDetection.AI.Internal;
using Granit.LanguageDetection.AI.Options;
using Granit.LanguageDetection.AI.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        // ValidateOnStart aborts host boot when MaxContentLength / TimeoutSeconds /
        // MaxAICallsPerHourPerTenant / WorkspaceName are out of range. Silent
        // degradation here would inflate the `injections.detected` counter with false
        // positives (e.g. MaxContentLength=0 → empty sample → schema reject).
        services.AddOptions<LanguageDetectionAIOptions>()
            .BindConfiguration(LanguageDetectionAIOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<LanguageDetectionAIMetrics>();
        services.TryAddSingleton<IAILanguageDetectionPromptBuilder, DefaultAILanguageDetectionPromptBuilder>();

        // Plug AILanguageDetector into the composite chain as an additional provider.
        // The composite resolves IEnumerable<ILanguageDetectorProvider>; using
        // TryAddEnumerable keeps the registration idempotent for re-entrant DI builds.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILanguageDetectorProvider, AILanguageDetector>());

        // Warn at startup when RedactPIIBeforeLLMCall is enabled but the registered
        // IAIContentRedactor is the identity NoOpAIContentRedactor — otherwise the
        // option name is misleading and PII flows raw to the LLM with no masking.
        // Uses the shared Granit.AI.Extraction probe (same trap across every AI feature).
        services.AddAIRedactionStartupWarning(
            "Granit.LanguageDetection.AI",
            sp => sp.GetRequiredService<IOptions<LanguageDetectionAIOptions>>().Value.RedactPIIBeforeLLMCall);

        return services;
    }
}
