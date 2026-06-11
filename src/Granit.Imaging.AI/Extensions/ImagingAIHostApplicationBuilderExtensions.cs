using System.Diagnostics.CodeAnalysis;
using Granit.Imaging.AI.Diagnostics;
using Granit.Imaging.AI.Internal;
using Granit.Imaging.AI.Options;

namespace Granit.Imaging.AI.Extensions;

/// <summary>
/// Extension methods for registering AI-powered image analysis services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ImagingAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds AI-powered image analysis services to the application.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="ImagingAIOptions"/> from the <c>Imaging:AI</c> configuration section
    /// and registers <see cref="IAIImageAnalyzer"/> backed by a multimodal LLM.
    /// Requires <c>Granit.AI</c> to be registered with a vision-capable provider.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitImagingAI(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(ImagingAIActivitySource.Name);

        builder.Services
            .AddOptions<ImagingAIOptions>()
            .BindConfiguration(ImagingAIOptions.SectionName);

        builder.Services.TryAddSingleton<ImagingAIMetrics>();
        // Scoped because LlmImageAnalyzer depends on IAIChatClientFactory (scoped).
        // The analyzer carries no singleton-justifying state.
        builder.Services.TryAddScoped<IAIImageAnalyzer, LlmImageAnalyzer>();

        return builder;
    }
}
