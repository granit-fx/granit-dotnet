using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;

namespace Granit.Scheduling;

/// <summary>
/// Persists scheduled action changes (command side of CQRS).
/// </summary>
public interface IScheduledActionWriter
{
    /// <summary>
    /// Persists a new scheduled action.
    /// </summary>
    /// <param name="action">The scheduled action to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(ScheduledAction action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing scheduled action.
    /// </summary>
    /// <param name="action">The modified scheduled action.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(ScheduledAction action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a pending action for execution by transitioning its status
    /// from <see cref="ScheduledActionStatus.Pending"/> to <see cref="ScheduledActionStatus.Processing"/>.
    /// </summary>
    /// <remarks>
    /// Uses an atomic database update (<c>ExecuteUpdateAsync</c> with a <c>WHERE Status = Pending</c>
    /// guard) to prevent double-execution when concurrent dispatches target the same action.
    /// Only one caller wins the claim — all others receive <c>false</c>.
    /// </remarks>
    /// <param name="id">The scheduled action identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the action was successfully claimed; <c>false</c> if it was already claimed or not found.</returns>
    Task<bool> TryClaimForExecutionAsync(ScheduledActionId id, CancellationToken cancellationToken = default);
}
