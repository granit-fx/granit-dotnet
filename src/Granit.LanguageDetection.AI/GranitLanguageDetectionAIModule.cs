using Granit.AI.Extraction;
using Granit.LanguageDetection.AI.Extensions;
using Granit.Modularity;

namespace Granit.LanguageDetection.AI;

/// <summary>
/// Granit module for the AI-backed language detector. Registers
/// <c>AILanguageDetector</c> as an additional provider in the language-detection
/// composite chain at priority <c>200</c>.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitAIExtractionModule"/> (for the rate limiter +
/// content-redactor seam) and <see cref="GranitLanguageDetectionModule"/> (for the
/// provider marker + composite). Hosts wire in a concrete AI provider package
/// (<c>Granit.AI.OpenAI</c>, <c>Granit.AI.Anthropic</c>, …) separately.
/// </remarks>
[DependsOn(typeof(GranitAIExtractionModule), typeof(GranitLanguageDetectionModule))]
public sealed class GranitLanguageDetectionAIModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitLanguageDetectionAI();
}
