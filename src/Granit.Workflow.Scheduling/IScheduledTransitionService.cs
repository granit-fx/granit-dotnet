using Granit.Scheduling.Domain.ValueObjects;

namespace Granit.Workflow.Scheduling;

/// <summary>
/// Schedules, cancels and reschedules time-based workflow transitions for a
/// workflow-stateful entity. The single entry point a consumer needs to make
/// "transition this entity to state X at instant T" trivial.
/// </summary>
/// <remarks>
/// <para>
/// A transition is keyed by <c>(workflowEntityType, entityId)</c>: the service derives a
/// deterministic correlation identifier (<c>workflow:{workflowEntityType}:{entityId}</c>)
/// so a pending transition can be found and cancelled or rescheduled when an editor changes
/// the date — without the caller tracking a <see cref="ScheduledActionId"/>.
/// </para>
/// <para>
/// <c>executeAt</c> is an <strong>absolute instant</strong>. Converting a wall
/// time in a site's IANA time zone (e.g. "2026-08-01 09:00 in <c>Europe/Brussels</c>") to an
/// instant is the caller's responsibility — use <c>Granit.Timing</c>. This service stays
/// zone-agnostic and minimal.
/// </para>
/// <para>
/// Authorization happens here, at scheduling time (the calling endpoint is permission-gated).
/// The transition later executes in system context and is not re-authorized — see
/// <see cref="IWorkflowTransitionApplier"/>.
/// </para>
/// </remarks>
public interface IScheduledTransitionService
{
    /// <summary>
    /// Schedules a transition of the entity to <paramref name="targetState"/> at
    /// <paramref name="executeAt"/>.
    /// </summary>
    /// <param name="workflowEntityType">
    /// The logical workflow entity type (matching <c>IWorkflowStateful.WorkflowEntityType</c>
    /// and the applier registration key), e.g. <c>"BlogPost"</c>.
    /// </param>
    /// <param name="entityId">The identifier of the entity to transition.</param>
    /// <param name="targetState">The target workflow state name, e.g. <c>"Published"</c>.</param>
    /// <param name="executeAt">The absolute instant at which the transition should run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identifier of the created scheduled action.</returns>
    Task<ScheduledActionId> ScheduleTransitionAsync(
        string workflowEntityType,
        Guid entityId,
        string targetState,
        DateTimeOffset executeAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any pending scheduled transition for the entity. No-op when none is pending.
    /// </summary>
    /// <param name="workflowEntityType">The logical workflow entity type.</param>
    /// <param name="entityId">The identifier of the entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelScheduledTransitionAsync(
        string workflowEntityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules the entity's transition to a new instant (and optionally a new target
    /// state). Any pending transition for the entity is cancelled and a fresh one is
    /// scheduled, so the call is safe whether or not a transition is currently pending.
    /// </summary>
    /// <param name="workflowEntityType">The logical workflow entity type.</param>
    /// <param name="entityId">The identifier of the entity.</param>
    /// <param name="targetState">The target workflow state name.</param>
    /// <param name="newExecuteAt">The new absolute instant at which the transition should run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identifier of the newly created scheduled action.</returns>
    Task<ScheduledActionId> RescheduleTransitionAsync(
        string workflowEntityType,
        Guid entityId,
        string targetState,
        DateTimeOffset newExecuteAt,
        CancellationToken cancellationToken = default);
}
