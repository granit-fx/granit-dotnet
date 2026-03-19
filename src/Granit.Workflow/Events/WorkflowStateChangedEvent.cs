using Granit.Core.Events;

namespace Granit.Workflow.Events;

/// <summary>
/// Non-generic domain event published when a workflow state transition completes.
/// Unlike <see cref="WorkflowTransitionedEvent{TState}"/> (generic, app-published),
/// this event uses string states for framework-level consumption by notification handlers.
/// </summary>
/// <param name="EntityType">The entity type (e.g. "Publication", "Invoice").</param>
/// <param name="EntityId">The entity identifier.</param>
/// <param name="PreviousState">Previous state name (enum value as string).</param>
/// <param name="NewState">New state name (enum value as string).</param>
/// <param name="TransitionedBy">User ID who triggered the transition.</param>
public sealed record WorkflowStateChangedEvent(
    string EntityType,
    string EntityId,
    string PreviousState,
    string NewState,
    string TransitionedBy) : IDomainEvent;
