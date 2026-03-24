using Granit.Events;

namespace Granit.Workflow.Events;

/// <summary>
/// Domain event published when a workflow state transition completes successfully.
/// Routed to the Wolverine local <c>domain-events</c> queue (in-process, transactional).
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public sealed record WorkflowTransitionedEvent<TState>(
    string EntityType,
    string EntityId,
    TState PreviousState,
    TState NewState,
    string TransitionedBy) : IDomainEvent
    where TState : struct, Enum;
