using Granit.AI;

namespace Granit.Privacy.AI;

/// <summary>
/// Granit module for AI-powered PII detection.
/// </summary>
/// <remarks>
/// Provides <see cref="IAIPiiDetector"/> for scanning text for personally identifiable
/// information using LLM. Requires a registered AI provider (e.g. <c>Granit.AI.Ollama</c>
/// for local inference or <c>Granit.AI.AzureOpenAI</c> with DPA) to function.
/// <para>
/// PII detection data should not leave the security perimeter. Configure a local model
/// (Ollama) or a provider covered by a Data Processing Agreement (Azure OpenAI with DPA)
/// in the <c>Privacy:AI</c> workspace.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitAIModule), typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyAIModule : GranitModule;
