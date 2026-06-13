using Granit.AI.Tools;
using Granit.Imaging.AI.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Imaging.AI.Extensions;

/// <summary>
/// Registers the <c>extract_text_from_image</c> vision tool on the Granit AI tool registry.
/// </summary>
public static class ExtractTextFromImageToolRegistrationExtensions
{
    /// <summary>
    /// Opts the <c>extract_text_from_image</c> tool in (default-off). The tool reads images via a
    /// Vision-capable workspace and resolves image bytes through the registered
    /// <see cref="IAIImageSource"/>; if neither is configured it degrades gracefully.
    /// </summary>
    /// <param name="tools">The AI tool registration builder.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <example>
    /// <code>services.AddGranitAITools(tools => tools.AddImageTextExtractionTool());</code>
    /// </example>
    public static AIToolRegistrationBuilder AddImageTextExtractionTool(this AIToolRegistrationBuilder tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        tools.Services.AddScoped<IAITool, ExtractTextFromImageTool>();
        return tools;
    }
}
