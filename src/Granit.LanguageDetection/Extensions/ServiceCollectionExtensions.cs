using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.LanguageDetection.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.LanguageDetection</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the composite language detector. Concrete detectors (trigram,
    /// AI-backed, metadata-hint) plug in by registering additional
    /// <see cref="ILanguageDetector"/> services in DI; the composite resolves them
    /// at construction.
    /// </summary>
    public static IServiceCollection AddGranitLanguageDetection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<CompositeLanguageDetector>();
        services.TryAddSingleton<ILanguageDetector>(sp => sp.GetRequiredService<CompositeLanguageDetector>());

        return services;
    }
}
