namespace Granit.Workflow.Endpoints.Dtos;

/// <summary>
/// Request body for triggering a workflow transition.
/// </summary>
/// <param name="TargetState">Target state name to transition to.</param>
/// <param name="Comment">Optional regulatory comment or justification (ISO 27001 audit trail).</param>
public sealed record WorkflowTransitionRequest(
    string TargetState,
    string? Comment = null);
