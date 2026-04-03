using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;

namespace Granit.Scheduling;

/// <summary>
/// Reads scheduled action data (query side of CQRS).
/// </summary>
public interface IScheduledActionReader
{
    /// <summary>
    /// Returns a scheduled action by its identifier, or <c>null</c> if not found.
    /// </summary>
    /// <param name="id">The scheduled action identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scheduled action, or <c>null</c>.</returns>
    Task<ScheduledAction?> GetByIdAsync(ScheduledActionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all pending actions whose execution date has passed.
    /// Used by the catch-up safety net to detect overdue actions.
    /// </summary>
    /// <param name="overdueThreshold">Only actions with <c>ExecuteAt</c> before this timestamp are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Overdue pending actions.</returns>
    Task<IReadOnlyList<ScheduledAction>> GetOverduePendingAsync(
        DateTimeOffset overdueThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns scheduled actions matching the specified correlation identifier.
    /// </summary>
    /// <param name="correlationId">The correlation identifier (e.g., <c>"subscription:{id}"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching scheduled actions.</returns>
    Task<IReadOnlyList<ScheduledAction>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns scheduled actions filtered by status.
    /// </summary>
    /// <param name="status">Optional status filter. When <c>null</c>, returns actions in all statuses.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching scheduled actions.</returns>
    Task<IReadOnlyList<ScheduledAction>> GetByStatusAsync(
        ScheduledActionStatus? status,
        CancellationToken cancellationToken = default);
}
