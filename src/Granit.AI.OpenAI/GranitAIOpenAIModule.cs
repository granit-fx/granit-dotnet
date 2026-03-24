using Granit.Modularity;

namespace Granit.AI.OpenAI;

/// <summary>
/// Granit module for the OpenAI AI provider.
/// </summary>
/// <remarks>
/// Registers <c>OpenAIProviderFactory</c> as <see cref="IAIProviderFactory"/> when
/// <see cref="Extensions.AIOpenAIHostApplicationBuilderExtensions.AddGranitAIOpenAI"/>
/// is called.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIOpenAIModule : GranitModule;
