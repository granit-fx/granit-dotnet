using Granit.Diagnostics;
using Granit.LanguageDetection.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.LanguageDetection.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.LanguageDetection</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the composite language detector as the sole
    /// <see cref="ILanguageDetector"/>, the diagnostics metrics, and the
    /// <c>Granit.LanguageDetection</c> activity source. Concrete detectors (trigram,
    /// AI-backed, metadata-hint) plug in by registering additional
    /// <see cref="ILanguageDetectorProvider"/> services in DI (typically via
    /// <c>TryAddEnumerable</c>); the composite resolves them at construction.
    /// </summary>
    /// <remarks>
    /// Any prior <see cref="ILanguageDetector"/> binding is removed: the composite is
    /// the only sanctioned facade. Custom detectors MUST register under
    /// <see cref="ILanguageDetectorProvider"/> so they fan into the priority chain
    /// instead of silently replacing it (CWE-1188, secure defaults).
    /// </remarks>
    public static IServiceCollection AddGranitLanguageDetection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(LanguageDetectionActivitySource.Name);

        // Strip any pre-existing ILanguageDetector binding so a stray
        // services.AddSingleton<ILanguageDetector, MyDetector>() cannot silently
        // bypass the composite chain. Custom detectors register as
        // ILanguageDetectorProvider (the documented extension point).
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(ILanguageDetector))
            {
                services.RemoveAt(i);
            }
        }

        services.TryAddSingleton<LanguageDetectionMetrics>();
        services.TryAddSingleton<CompositeLanguageDetector>();
        services.AddSingleton<ILanguageDetector>(sp => sp.GetRequiredService<CompositeLanguageDetector>());

        return services;
    }
}
