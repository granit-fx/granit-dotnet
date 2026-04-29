namespace Granit.Workflow;

/// <summary>
/// Immutable definition of a finite state machine (FSM) workflow.
/// Describes the initial state and all allowed transitions between states.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public interface IWorkflowDefinition<TState> where TState : struct, Enum
{
    /// <summary>The state assigned to newly created entities.</summary>
    TState InitialState { get; }

    /// <summary>All transitions defined in this workflow.</summary>
    IReadOnlyList<WorkflowTransition<TState>> Transitions { get; }

    /// <summary>
    /// Returns the transitions available from the given <paramref name="from"/> state.
    /// Does not check user permissions — use <see cref="IWorkflowManager{TState}"/>
    /// for permission-aware transition queries.
    /// </summary>
    IReadOnlyList<WorkflowTransition<TState>> GetAllowedTransitions(TState from);
}
