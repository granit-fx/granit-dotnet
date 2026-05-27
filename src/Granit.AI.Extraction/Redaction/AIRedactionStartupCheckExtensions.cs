using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.AI.Extraction.Redaction;

/// <summary>
/// Registration helper for the shared <see cref="AIRedactionStartupCheck"/>.
/// </summary>
public static class AIRedactionStartupCheckExtensions
{
    /// <summary>
    /// Registers a startup probe that warns when <paramref name="redactionEnabled"/>
    /// resolves to <c>true</c> while the only registered <see cref="IAIContentRedactor"/>
    /// is the identity <see cref="NoOpAIContentRedactor"/> — i.e. the feature believes it
    /// is masking PII before the LLM call but is not.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="featureName">
    /// Human-readable feature name used in the warning (e.g.
    /// <c>"Granit.LanguageDetection.AI"</c>).
    /// </param>
    /// <param name="redactionEnabled">
    /// Resolves the feature's "redact before LLM call" flag from the container at startup
    /// (typically reads the feature's bound options).
    /// </param>
    public static IServiceCollection AddAIRedactionStartupWarning(
        this IServiceCollection services,
        string featureName,
        Func<IServiceProvider, bool> redactionEnabled)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(featureName);
        ArgumentNullException.ThrowIfNull(redactionEnabled);

        services.AddSingleton<IHostedService>(sp => new AIRedactionStartupCheck(
            featureName,
            () => redactionEnabled(sp),
            sp.GetRequiredService<IAIContentRedactor>(),
            sp.GetRequiredService<ILogger<AIRedactionStartupCheck>>()));

        return services;
    }
}
