using Granit.Scheduling;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;

namespace Granit.Workflow.Scheduling.Internal;

/// <summary>
/// Default <see cref="IScheduledTransitionService"/> that delegates to <see cref="IScheduler"/>
/// and keys scheduled transitions by a deterministic correlation identifier.
/// </summary>
internal sealed class DefaultScheduledTransitionService(
    IScheduler scheduler,
    IScheduledActionReader actionReader) : IScheduledTransitionService
{
    /// <inheritdoc/>
    public Task<ScheduledActionId> ScheduleTransitionAsync(
        string workflowEntityType,
        Guid entityId,
        string targetState,
        DateTimeOffset executeAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetState);

        string correlationId = BuildCorrelationId(workflowEntityType, entityId);
        var payload = new ScheduledWorkflowTransitionPayload(workflowEntityType, entityId, targetState, correlationId);

        return scheduler.ScheduleAsync(payload, executeAt, correlationId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CancelScheduledTransitionAsync(
        string workflowEntityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityType);

        string correlationId = BuildCorrelationId(workflowEntityType, entityId);
        IReadOnlyList<ScheduledAction> actions = await actionReader
            .GetByCorrelationIdAsync(correlationId, cancellationToken)
            .ConfigureAwait(false);

        foreach (ScheduledAction action in actions)
        {
            if (action.Status == ScheduledActionStatus.Pending)
            {
                await scheduler.CancelAsync(ScheduledActionId.Create(action.Id), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ScheduledActionId> RescheduleTransitionAsync(
        string workflowEntityType,
        Guid entityId,
        string targetState,
        DateTimeOffset newExecuteAt,
        CancellationToken cancellationToken = default)
    {
        // Cancel-then-schedule: robust whether or not a transition is pending, and correct when
        // the editor changes the target state as well as the date.
        await CancelScheduledTransitionAsync(workflowEntityType, entityId, cancellationToken).ConfigureAwait(false);
        return await ScheduleTransitionAsync(workflowEntityType, entityId, targetState, newExecuteAt, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the deterministic correlation identifier <c>workflow:{workflowEntityType}:{entityId}</c>
    /// used to find an entity's pending transition for cancel/reschedule.
    /// </summary>
    internal static string BuildCorrelationId(string workflowEntityType, Guid entityId) =>
        $"workflow:{workflowEntityType}:{entityId}";
}
