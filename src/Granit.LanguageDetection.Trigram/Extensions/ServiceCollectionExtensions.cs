using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.LanguageDetection.Trigram.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.LanguageDetection.Trigram</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TrigramLanguageDetector"/> as an additional
    /// <see cref="ILanguageDetectorProvider"/> in the DI container. The composite chain
    /// wired by <c>AddGranitLanguageDetection</c> picks it up at
    /// <see cref="TrigramLanguageDetector.Priority"/>.
    /// </summary>
    public static IServiceCollection AddGranitLanguageDetectionTrigram(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TrigramLanguageDetector>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILanguageDetectorProvider, TrigramLanguageDetector>(
            sp => sp.GetRequiredService<TrigramLanguageDetector>()));

        return services;
    }
}
