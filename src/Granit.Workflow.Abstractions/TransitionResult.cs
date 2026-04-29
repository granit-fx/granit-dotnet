namespace Granit.Workflow;

/// <summary>
/// Result of a workflow transition attempt via <see cref="IWorkflowManager{TState}"/>.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public sealed record TransitionResult<TState> where TState : struct, Enum
{
    /// <summary>Whether the transition was executed (either directly or via approval routing).</summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// The state after the transition attempt. May differ from the requested target
    /// when approval routing redirects to a pending review state.
    /// </summary>
    public required TState ResultingState { get; init; }

    /// <summary>The outcome classification of the transition attempt.</summary>
    public required TransitionOutcome Outcome { get; init; }
}

/// <summary>
/// Classifies the outcome of a workflow transition attempt.
/// </summary>
public enum TransitionOutcome
{
    /// <summary>Transition succeeded — entity moved to the requested target state.</summary>
    Completed,

    /// <summary>
    /// Approval routing — entity moved to a pending review state because the user
    /// lacks the required permission. Approvers have been notified.
    /// </summary>
    ApprovalRequested,

    /// <summary>
    /// Denied — the user lacks the required permission and the transition
    /// does not support approval routing.
    /// </summary>
    Denied,

    /// <summary>
    /// Invalid transition — no transition is defined from the current state
    /// to the requested target state.
    /// </summary>
    InvalidTransition,
}
