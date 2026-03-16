namespace Granit.Workflow.AI;

/// <summary>
/// Evaluates the risk of a workflow transition for auto-approval decisions using AI.
/// </summary>
/// <remarks>
/// <para>
/// When a transition requires approval, this evaluator can assess the risk level to determine
/// whether the transition can be auto-approved (low risk) or requires human review (high risk).
/// </para>
/// <para>
/// The evaluator never executes transitions — it only assesses risk. The human (or a
/// threshold-based policy) decides whether to auto-approve.
/// </para>
/// </remarks>
public interface IAIApprovalEvaluator
{
    /// <summary>
    /// Evaluates the risk of a workflow transition.
    /// </summary>
    /// <param name="entityType">The type of entity (e.g. "Invoice", "Document").</param>
    /// <param name="transition">The name of the transition being evaluated.</param>
    /// <param name="entityContext">
    /// Contextual information about the entity (e.g. JSON representation, summary).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A risk assessment with score, reasoning, and identified risk factors.</returns>
    Task<RiskAssessment> EvaluateRiskAsync(
        string entityType,
        string transition,
        string entityContext,
        CancellationToken cancellationToken = default);
}
