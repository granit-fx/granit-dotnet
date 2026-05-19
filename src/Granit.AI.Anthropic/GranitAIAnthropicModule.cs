using Granit.Modularity;

namespace Granit.AI.Anthropic;

/// <summary>
/// Granit module for the Anthropic (Claude) AI provider.
/// </summary>
/// <remarks>
/// Registers <c>AnthropicProviderFactory</c> as <see cref="IAIProviderFactory"/> when
/// <see cref="Extensions.AIAnthropicHostApplicationBuilderExtensions.AddGranitAIAnthropic"/>
/// is called. Claude models (Opus, Sonnet, Haiku) are exposed as chat completion providers;
/// embedding generation is not supported.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIAnthropicModule : GranitModule;
