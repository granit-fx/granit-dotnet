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
    /// Transitions a request to <see cref="DeletionRequestState.Executing"/> — the deadline was
    /// reached and the provider fan-out has begun, but no provider has acknowledged yet.
    /// The default implementation is a no-op so pre-existing trackers keep compiling; the
    /// request simply stays in its prior state until an acknowledgement or the timeout resolves
    /// it. Implementations SHOULD override to record the intermediate state, otherwise a request
    /// whose providers all fail is indistinguishable from one still deferred.
    /// </summary>
    Task MarkExecutingAsync(Guid requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Transitions a request to <see cref="DeletionRequestState.PartiallyExecuted"/> — the
    /// acknowledgement window elapsed before every provider confirmed erasure. The
    /// <paramref name="missingProviders"/> list names the providers that never acknowledged so an
    /// operator can reconcile them (their handlers may have permanently failed or dead-lettered).
    /// The default implementation delegates to <see cref="MarkExecutedAsync"/> so pre-existing
    /// trackers degrade to the legacy "mark executed" behaviour; implementations SHOULD override
    /// to persist the partial state and the missing-provider set for GDPR Art. 17 provability.
    /// </summary>
    Task MarkPartiallyExecutedAsync(
        Guid requestId,
        DateTimeOffset executedAt,
        IReadOnlyList<string> missingProviders,
        CancellationToken cancellationToken = default) =>
        MarkExecutedAsync(requestId, executedAt, cancellationToken);

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
