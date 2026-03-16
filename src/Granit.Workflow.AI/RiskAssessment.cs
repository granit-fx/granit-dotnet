namespace Granit.Workflow.AI;

/// <summary>
/// Result of an AI-powered risk evaluation for a workflow transition.
/// </summary>
/// <param name="RiskScore">
/// Risk score between 0.0 (no risk) and 1.0 (highest risk).
/// A score below <see cref="Options.WorkflowAIOptions.AutoApprovalThreshold"/>
/// indicates the transition is safe for auto-approval.
/// </param>
/// <param name="Reasoning">
/// Human-readable explanation of the risk assessment.
/// </param>
/// <param name="RiskFactors">
/// Individual risk factors identified during the evaluation.
/// </param>
public sealed record RiskAssessment(
    double RiskScore,
    string Reasoning,
    IReadOnlyList<string> RiskFactors);
