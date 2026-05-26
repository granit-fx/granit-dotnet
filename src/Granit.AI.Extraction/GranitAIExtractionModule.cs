using Granit.AI.Extraction.Extensions;
using Granit.Modularity;

namespace Granit.AI.Extraction;

/// <summary>
/// Granit module for AI-powered structured document extraction.
/// </summary>
/// <remarks>
/// Provides <see cref="IDocumentExtractor{TResult}"/> for extracting typed C# objects
/// from document text using LLM with JSON structured output. Also registers the
/// cross-cutting AI infrastructure consumed by every AI-feature package downstream
/// (rate limiter, content-redactor seam). Requires a registered AI provider
/// (e.g. <c>Granit.AI.OpenAI</c>) to function.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIExtractionModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        AIExtractionServiceCollectionExtensions.AddGranitAIExtractionCore(context.Services);
}
