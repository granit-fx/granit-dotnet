using Granit.AI;
using Granit.Core.Modularity;
using Granit.Templating;

namespace Granit.Templating.AI;

/// <summary>
/// Granit module that provides AI-powered template assistance.
/// </summary>
/// <remarks>
/// When this module is installed, an LLM-backed <see cref="IAITemplateAssistant"/> is available
/// to generate Scriban template drafts from natural language descriptions.
/// <para>
/// The <see cref="AITemplateDataEnricher{TData}"/> base class enables AI-driven data enrichment
/// (summaries, translations, recommendations) in the template data pipeline.
/// </para>
/// <para>
/// Only data schema metadata is sent to the LLM for template generation — never business data (GDPR safe).
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitTemplatingAIModule : GranitModule;
