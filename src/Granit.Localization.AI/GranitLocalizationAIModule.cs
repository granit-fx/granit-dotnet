using Granit.AI;
using Granit.AI.Tools;
using Granit.Modularity;

namespace Granit.Localization.AI;

/// <summary>
/// Granit module for AI-powered translation suggestions.
/// </summary>
/// <remarks>
/// Provides <see cref="ITranslationSuggestionService"/> for generating translation suggestions
/// across the 17 supported cultures using an LLM. Requires a registered AI provider
/// (e.g. <c>Granit.AI.OpenAI</c>) to function.
/// </remarks>
[DependsOn(typeof(GranitAIModule), typeof(GranitAIToolsModule), typeof(GranitLocalizationModule))]
public sealed class GranitLocalizationAIModule : GranitModule;
