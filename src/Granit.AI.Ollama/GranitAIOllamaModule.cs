using Granit.Http.Resilience;
using Granit.Modularity;

namespace Granit.AI.Ollama;

/// <summary>
/// Granit module for the Ollama AI provider.
/// </summary>
/// <remarks>
/// Registers <c>OllamaProviderFactory</c> as <see cref="IAIProviderFactory"/> when
/// <see cref="Extensions.AIOllamaHostApplicationBuilderExtensions.AddGranitAIOllama"/>
/// is called. Supports local model inference via Ollama for development,
/// on-premise deployments, and GDPR-compliant data processing.
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitHttpResilienceModule))]
public sealed class GranitAIOllamaModule : GranitModule;
