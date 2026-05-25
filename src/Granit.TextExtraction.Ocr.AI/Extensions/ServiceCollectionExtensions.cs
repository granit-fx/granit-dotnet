using Granit.TextExtraction.Extensions;
using Granit.TextExtraction.Ocr.AI.Internal;
using Granit.TextExtraction.Ocr.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.TextExtraction.Ocr.AI.Extensions;

/// <summary>
/// Extension methods for registering the AI vision OCR extractor.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AIVisionOcrExtractor"/> with the <c>Granit.TextExtraction</c>
    /// pipeline. Implicitly calls <c>AddGranitTextExtraction()</c>, binds
    /// <see cref="AIVisionOcrOptions"/>, and registers the default
    /// <see cref="IVisionOcrPromptBuilder"/>. The host MUST also register a Granit.AI
    /// workspace (via <c>builder.AddGranitAI()</c> + a provider package) — this method
    /// does not configure the AI module itself.
    /// </summary>
    public static IServiceCollection AddAIVisionOcrExtractor(
        this IServiceCollection services,
        Action<AIVisionOcrOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddTextExtractor<AIVisionOcrExtractor>();

        services.TryAddSingleton<IVisionOcrPromptBuilder, DefaultVisionOcrPromptBuilder>();

        services.AddOptions<AIVisionOcrOptions>()
            .BindConfiguration(AIVisionOcrOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
