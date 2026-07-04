using Granit.Scheduling;

namespace Granit.Workflow.Scheduling;

/// <summary>
/// A scheduled payload that transitions a workflow-stateful entity to a target state at a
/// future instant. Persisted as JSON by <c>Granit.Scheduling</c> and delivered to
/// <see cref="ScheduledWorkflowTransitionHandler"/> at execution time.
/// </summary>
/// <remarks>
/// <para>
/// The payload is deliberately <strong>non-generic</strong>: the target state travels as a
/// string and the concrete <c>TState</c> enum is resolved by the entity-type-specific
/// <see cref="IWorkflowTransitionApplier"/> at execution time. This keeps dispatch, storage
/// and the Wolverine handler free of any generic type parameter while confining the generic
/// complexity to each applier.
/// </para>
/// <para>
/// Scheduled via <see cref="IScheduledTransitionService"/>, which stamps a deterministic
/// <see cref="CorrelationId"/> (<c>workflow:{WorkflowEntityType}:{EntityId}</c>) so the
/// action can be cancelled or rescheduled by entity when an editor changes the date.
/// </para>
/// </remarks>
/// <param name="WorkflowEntityType">
/// The logical workflow entity type, matching <c>IWorkflowStateful.WorkflowEntityType</c> and
/// the key under which the target <see cref="IWorkflowTransitionApplier"/> is registered
/// (e.g. <c>"BlogPost"</c>).
/// </param>
/// <param name="EntityId">The identifier of the entity to transition.</param>
/// <param name="TargetState">
/// The name of the target workflow state (the <c>TState</c> enum member name, e.g.
/// <c>"Published"</c>). Parsed to the concrete enum by the applier.
/// </param>
/// <param name="CorrelationId">
/// Optional correlation identifier linking the scheduled action to its entity. Set by
/// <see cref="IScheduledTransitionService"/> to the deterministic
/// <c>workflow:{WorkflowEntityType}:{EntityId}</c> form.
/// </param>
public sealed record ScheduledWorkflowTransitionPayload(
    string WorkflowEntityType,
    Guid EntityId,
    string TargetState,
    string? CorrelationId = null) : IScheduledPayload;
