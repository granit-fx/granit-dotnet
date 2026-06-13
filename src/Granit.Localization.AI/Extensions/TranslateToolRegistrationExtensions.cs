using Granit.AI.Tools;
using Granit.Localization.AI.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Localization.AI.Extensions;

/// <summary>
/// Registers the <c>translate</c> capability tool on the Granit AI tool registry.
/// </summary>
public static class TranslateToolRegistrationExtensions
{
    /// <summary>
    /// Opts the <c>translate</c> tool in. It is gated by <c>AI.ChatTools.Translate</c>, so it is
    /// only offered to users granted that permission. Requires <c>ITranslationSuggestionService</c>
    /// (Localization.AI) to be registered.
    /// </summary>
    /// <param name="tools">The AI tool registration builder.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <example>
    /// <code>services.AddGranitAITools(tools => tools.AddTranslateTool());</code>
    /// </example>
    public static AIToolRegistrationBuilder AddTranslateTool(this AIToolRegistrationBuilder tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        tools.Services.AddScoped<IAITool, TranslateTool>();
        return tools;
    }
}
