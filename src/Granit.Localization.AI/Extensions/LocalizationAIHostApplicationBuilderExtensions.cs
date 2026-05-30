using System.Diagnostics.CodeAnalysis;
using Granit.Localization.AI.Internal;
using Granit.Localization.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Localization.AI.Extensions;

/// <summary>
/// Extension methods for registering Granit AI localization services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class LocalizationAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Granit AI translation suggestion services and binds <see cref="LocalizationAIOptions"/>
    /// from the <c>AI:Localization</c> configuration section.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitLocalizationAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<LocalizationAIOptions>()
            .BindConfiguration(LocalizationAIOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Scoped, not singleton: the service depends on the scoped IStructuredCompletion
        // primitive (ADR-064), so a singleton here would capture a stale scope.
        builder.Services.TryAddScoped<ITranslationSuggestionService, LlmTranslationSuggestionService>();

        return builder;
    }
}
