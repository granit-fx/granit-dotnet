using Granit.AI;
using Granit.Core.Modularity;

namespace Granit.AI.Extraction;

/// <summary>
/// Granit module for AI-powered structured document extraction.
/// </summary>
/// <remarks>
/// Provides <see cref="IDocumentExtractor{TResult}"/> for extracting typed C# objects
/// from document text using LLM with JSON structured output. Requires a registered
/// AI provider (e.g. <c>Granit.AI.OpenAI</c>) to function.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIExtractionModule : GranitModule;
