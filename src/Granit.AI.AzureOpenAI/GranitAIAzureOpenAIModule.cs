using Granit.Modularity;

namespace Granit.AI.AzureOpenAI;

/// <summary>
/// Granit module for the Azure OpenAI provider.
/// </summary>
/// <remarks>
/// Registers <see cref="Internal.AzureOpenAIProviderFactory"/> when
/// <see cref="Extensions.AIAzureOpenAIHostApplicationBuilderExtensions.AddGranitAIAzureOpenAI"/>
/// is called. Supports both API key and Managed Identity authentication.
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIAzureOpenAIModule : GranitModule;
