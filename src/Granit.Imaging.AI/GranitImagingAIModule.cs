using Granit.AI;
using Granit.AI.Tools;
using Granit.Imaging.AI.Extensions;
using Granit.Modularity;

namespace Granit.Imaging.AI;

/// <summary>
/// Granit module for AI-powered image analysis.
/// </summary>
/// <remarks>
/// Bridges <c>Granit.Imaging</c> and <c>Granit.AI</c> to provide multimodal LLM-based
/// image analysis: classification, OCR, alt text generation.
/// <para>
/// Requires a multimodal AI provider (e.g. GPT-4o, Claude with vision) registered
/// via <c>Granit.AI</c> workspaces.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitAIModule), typeof(GranitAIToolsModule), typeof(GranitImagingModule))]
public sealed class GranitImagingAIModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitImagingAI();
}
