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
    /// Registers <see cref="AiVisionOcrExtractor"/> with the <c>Granit.TextExtraction</c>
    /// pipeline. Implicitly calls <c>AddGranitTextExtraction()</c>, binds
    /// <see cref="AiVisionOcrOptions"/>, and registers the default
    /// <see cref="IVisionOcrPromptBuilder"/>. The host MUST also register a Granit.AI
    /// workspace (via <c>builder.AddGranitAI()</c> + a provider package) — this method
    /// does not configure the AI module itself.
    /// </summary>
    public static IServiceCollection AddAiVisionOcrExtractor(
        this IServiceCollection services,
        Action<AiVisionOcrOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddTextExtractor<AiVisionOcrExtractor>();

        services.TryAddSingleton<IVisionOcrPromptBuilder, DefaultVisionOcrPromptBuilder>();

        services.AddOptions<AiVisionOcrOptions>()
            .BindConfiguration(AiVisionOcrOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
