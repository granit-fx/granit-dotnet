namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Persists deferred deletion request state transitions (CQRS write side).
/// Application must provide a concrete implementation via
/// <see cref="GranitPrivacyBuilder.UseDeletionRequestTracker{TStore}"/>.
/// </summary>
public interface IDeletionRequestTrackerWriter
{
    /// <summary>Records a new deferred deletion request in <see cref="DeletionRequestState.Deferred"/> state.</summary>
    Task RecordDeferredAsync(
        Guid requestId,
        Guid userId,
        string reason,
        DateTimeOffset requestedAt,
        DateTimeOffset scheduledDeletionAt,
        CancellationToken cancellationToken = default);

    /// <summary>Transitions a request to <see cref="DeletionRequestState.Executed"/>.</summary>
    Task MarkExecutedAsync(Guid requestId, DateTimeOffset executedAt, CancellationToken cancellationToken = default);

    /// <summary>Transitions a request to <see cref="DeletionRequestState.Cancelled"/>.</summary>
    Task MarkCancelledAsync(Guid requestId, DateTimeOffset cancelledAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an immediate deletion (no grace period) in terminal <see cref="DeletionRequestState.Executed"/> state.
    /// Default implementation delegates to <see cref="RecordDeferredAsync"/> + <see cref="MarkExecutedAsync"/>
    /// for backward compatibility. Implementations may override for a single atomic operation.
    /// </summary>
    async Task RecordImmediateDeletionAsync(
        Guid requestId,
        Guid userId,
        string reason,
        DateTimeOffset executedAt,
        CancellationToken cancellationToken = default)
    {
        await RecordDeferredAsync(requestId, userId, reason, executedAt, executedAt, cancellationToken)
            .ConfigureAwait(false);
        await MarkExecutedAsync(requestId, executedAt, cancellationToken)
            .ConfigureAwait(false);
    }
}
