namespace Granit.Workflow.AI.Options;

/// <summary>
/// Configuration options for AI-powered workflow recommendations and risk evaluation.
/// </summary>
/// <remarks>
/// Bound to the <c>Workflow:AI</c> configuration section.
/// </remarks>
public sealed class WorkflowAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Workflow:AI";

    /// <summary>
    /// AI workspace name to use for workflow AI operations. Defaults to <c>"default"</c>.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Maximum time in seconds to wait for an AI operation to complete. Defaults to <c>10</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Risk score threshold below which a transition can be auto-approved.
    /// Defaults to <c>0.3</c> (low risk = auto-approve).
    /// </summary>
    /// <remarks>
    /// A <see cref="RiskAssessment.RiskScore"/> below this value indicates the transition
    /// is considered safe enough for automatic approval without human review.
    /// </remarks>
    public double AutoApprovalThreshold { get; set; } = 0.3;
}
