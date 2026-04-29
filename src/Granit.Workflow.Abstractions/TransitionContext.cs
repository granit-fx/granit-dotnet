namespace Granit.Workflow;

/// <summary>
/// Optional context passed to <see cref="IWorkflowManager{TState}.TransitionAsync"/>
/// to provide metadata for the transition (e.g. a regulatory comment).
/// </summary>
public sealed record TransitionContext
{
    /// <summary>
    /// Optional comment or regulatory justification for the transition.
    /// Stored in the <see cref="Domain.WorkflowTransitionRecord.Comment"/> field
    /// of the ISO 27001 audit trail via <see cref="WorkflowTransitionContext"/>.
    /// </summary>
    public string? Comment { get; init; }
}
