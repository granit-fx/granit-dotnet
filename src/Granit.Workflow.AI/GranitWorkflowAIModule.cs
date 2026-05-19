using Granit.AI;
using Granit.Modularity;

namespace Granit.Workflow.AI;

/// <summary>
/// Granit module for AI-powered workflow transition recommendations and risk evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Provides <see cref="IAITransitionAdvisor"/> for recommending the next transition based on
/// entity context, and <see cref="IAIApprovalEvaluator"/> for evaluating transition risk to
/// support auto-approval decisions.
/// </para>
/// <para>
/// The LLM never executes transitions — it only recommends. The human (or a threshold-based
/// policy) decides whether to proceed.
/// </para>
/// <para>
/// Requires a registered AI provider (e.g. <c>Granit.AI.OpenAI</c>) to function.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitWorkflowAIModule : GranitModule;
