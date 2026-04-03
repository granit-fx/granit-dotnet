using Granit.Scheduling.Domain.ValueObjects;

namespace Granit.Scheduling;

/// <summary>
/// Schedules typed actions for one-shot execution at a specific future date.
/// </summary>
/// <remarks>
/// Unlike <c>Granit.BackgroundJobs</c> (recurring cron), scheduled actions execute
/// exactly once at the specified time. Each action carries a typed payload that is
/// delivered to its Wolverine handler at execution time.
/// <para>
/// All operations are tenant-aware: scheduled actions inherit the current tenant context.
/// </para>
/// </remarks>
public interface IScheduler
{
    /// <summary>
    /// Schedules a typed payload for execution at the specified date.
    /// </summary>
    /// <typeparam name="TPayload">The payload type (must implement <see cref="IScheduledPayload"/>).</typeparam>
    /// <param name="payload">The payload to deliver at execution time.</param>
    /// <param name="executeAt">The UTC date and time when the payload should be delivered.</param>
    /// <param name="correlationId">
    /// Optional correlation identifier linking this action to a domain entity
    /// (e.g., <c>"subscription:3fa85f64-5717-4562-b3fc-2c963f66afa6"</c>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identifier of the created scheduled action.</returns>
    Task<ScheduledActionId> ScheduleAsync<TPayload>(
        TPayload payload,
        DateTimeOffset executeAt,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        where TPayload : IScheduledPayload;

    /// <summary>
    /// Cancels a pending scheduled action.
    /// </summary>
    /// <param name="id">The scheduled action identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the action is not in <c>Pending</c> status.
    /// </exception>
    Task CancelAsync(ScheduledActionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules a pending action to a new execution date.
    /// </summary>
    /// <param name="id">The scheduled action identifier.</param>
    /// <param name="newExecuteAt">The new UTC execution date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the action is not in <c>Pending</c> status.
    /// </exception>
    Task RescheduleAsync(
        ScheduledActionId id,
        DateTimeOffset newExecuteAt,
        CancellationToken cancellationToken = default);
}
