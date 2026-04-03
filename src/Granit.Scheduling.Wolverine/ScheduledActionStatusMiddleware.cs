using Granit.MultiTenancy;
using Granit.Scheduling.Diagnostics;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Granit.Timing;
using Wolverine;

namespace Granit.Scheduling.Wolverine;

/// <summary>
/// Wolverine middleware that automatically updates <see cref="ScheduledAction"/> status
/// after a scheduled payload handler executes.
/// </summary>
/// <remarks>
/// <para>
/// Applied to handlers whose message type implements <see cref="IScheduledPayload"/>.
/// Before execution, atomically claims the action (Pending → Processing) via
/// <see cref="IScheduledActionWriter.TryClaimForExecutionAsync"/> to prevent double-execution
/// when concurrent dispatches target the same action. After execution, marks the action
/// as <see cref="ScheduledActionStatus.Executed"/> or <see cref="ScheduledActionStatus.Failed"/>.
/// </para>
/// <para>
/// The <c>ScheduledActionId</c> is propagated via the Wolverine envelope header
/// <c>X-Scheduled-Action-Id</c>, set by <see cref="Internal.WolverineScheduler"/>.
/// </para>
/// </remarks>
public sealed class ScheduledActionStatusMiddleware(
    IScheduledActionReader actionReader,
    IScheduledActionWriter actionWriter,
    IClock clock,
    SchedulingMetrics metrics,
    ICurrentTenant currentTenant)
{
    /// <summary>Header name used to propagate the scheduled action ID across Wolverine messages.</summary>
    public const string ActionIdHeader = "X-Scheduled-Action-Id";

    /// <summary>
    /// Before handler execution: atomically claims the action (Pending → Processing).
    /// If the action was already claimed, cancelled, or executed, the handler is skipped.
    /// </summary>
    public async Task<HandlerContinuation> BeforeAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        if (!TryGetActionId(envelope, out ScheduledActionId? actionId))
        {
            return HandlerContinuation.Continue;
        }

        bool claimed = await actionWriter
            .TryClaimForExecutionAsync(actionId, cancellationToken)
            .ConfigureAwait(false);

        return claimed ? HandlerContinuation.Continue : HandlerContinuation.Stop;
    }

    /// <summary>
    /// After successful handler execution: marks the action as executed.
    /// </summary>
    public async Task AfterAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        if (!TryGetActionId(envelope, out ScheduledActionId? actionId))
        {
            return;
        }

        ScheduledAction? action = await actionReader.GetByIdAsync(actionId, cancellationToken).ConfigureAwait(false);
        if (action is null || action.Status != ScheduledActionStatus.Processing)
        {
            return;
        }

        action.MarkExecuted(clock.Now);
        await actionWriter.UpdateAsync(action, cancellationToken).ConfigureAwait(false);

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        metrics.RecordExecuted(tenantId, action.PayloadType.Split(',')[0].Split('.')[^1]);
    }

    /// <summary>
    /// After failed handler execution (all retries exhausted): marks the action as failed.
    /// </summary>
    public async Task PostProcessAsync(Envelope envelope, Exception exception, CancellationToken cancellationToken)
    {
        if (!TryGetActionId(envelope, out ScheduledActionId? actionId))
        {
            return;
        }

        ScheduledAction? action = await actionReader.GetByIdAsync(actionId, cancellationToken).ConfigureAwait(false);
        if (action is null || action.Status != ScheduledActionStatus.Processing)
        {
            return;
        }

        action.MarkFailed(exception.Message, clock.Now);
        await actionWriter.UpdateAsync(action, cancellationToken).ConfigureAwait(false);

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        metrics.RecordFailed(tenantId, action.PayloadType.Split(',')[0].Split('.')[^1]);
    }

    private static bool TryGetActionId(Envelope envelope, out ScheduledActionId actionId)
    {
        actionId = default!;

        if (envelope.Headers.TryGetValue(ActionIdHeader, out string? headerValue)
            && Guid.TryParse(headerValue, out Guid guid))
        {
            actionId = ScheduledActionId.Create(guid);
            return true;
        }

        return false;
    }
}
