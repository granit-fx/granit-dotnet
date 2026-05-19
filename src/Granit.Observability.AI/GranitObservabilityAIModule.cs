using Granit.AI;
using Granit.Modularity;
using Granit.Observability.AI.Extensions;

namespace Granit.Observability.AI;

/// <summary>
/// Granit module for AI-powered log analysis and anomaly detection.
/// </summary>
/// <remarks>
/// Provides <see cref="IAILogAnalyzer"/> for analyzing log batches using an LLM
/// to detect patterns, anomalies, and recurring issues.
/// <para>
/// Requires a configured AI workspace (via <c>Granit.AI</c>) and an active
/// observability stack (via <c>Granit.Observability</c>).
/// </para>
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
[DependsOn(typeof(GranitObservabilityModule))]
public sealed class GranitObservabilityAIModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitObservabilityAI();
}
