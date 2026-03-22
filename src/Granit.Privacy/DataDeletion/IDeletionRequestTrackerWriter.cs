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
}
