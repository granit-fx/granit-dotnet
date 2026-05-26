using Granit.AI.Extraction;
using Granit.Indexing.AI.Extensions;
using Granit.Modularity;

namespace Granit.Indexing.AI;

/// <summary>
/// Granit module for the AI-backed summarizer (and future AI auto-tagger).
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitAIExtractionModule"/> (for the rate limiter +
/// content-redactor seam) and <see cref="GranitIndexingModule"/> (for the
/// <see cref="ISummarizer"/> contract). Hosts wire in a concrete AI provider package
/// (<c>Granit.AI.OpenAI</c>, <c>Granit.AI.Anthropic</c>, …) separately.
/// </remarks>
[DependsOn(typeof(GranitAIExtractionModule), typeof(GranitIndexingModule))]
public sealed class GranitIndexingAIModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIndexingAISummarizer();
}
