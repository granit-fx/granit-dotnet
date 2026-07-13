namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Application service that orchestrates personal-data deletion requests (GDPR Art. 17):
/// the duplicate-request guard, grace-period arithmetic, deferred-vs-immediate branching,
/// the distributed-event sequencing, and the tracker / metrics writes. Extracted from the
/// HTTP handler so the workflow lives in the domain layer and is unit-testable in isolation.
/// </summary>
public interface IPrivacyDeletionRequestService
{
    /// <summary>
    /// Requests deletion for a data subject. When <see cref="RequestDeletionCommand.Defer"/> is
    /// set a cooling-off period is started; otherwise deletion executes immediately.
    /// </summary>
    Task<RequestDeletionOutcome> RequestDeletionAsync(
        RequestDeletionCommand command, CancellationToken cancellationToken = default);

    /// <summary>Cancels a deferred deletion request during its grace period.</summary>
    Task<CancelDeletionOutcome> CancelDeletionAsync(
        Guid requestId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Input for <see cref="IPrivacyDeletionRequestService.RequestDeletionAsync"/>.</summary>
/// <param name="UserId">Data subject whose data is scheduled for deletion.</param>
/// <param name="RequestedBy">Human-readable actor label recorded on the event (typically the user's email).</param>
/// <param name="Defer">When <c>true</c>, start a grace period instead of deleting immediately.</param>
/// <param name="Reason">Free-text justification (may contain personal data — never logged).</param>
/// <param name="Regulation">Resolved regulation code (e.g. <c>EU_GDPR</c>), resolved at the call site.</param>
public sealed record RequestDeletionCommand(
    Guid UserId,
    string RequestedBy,
    bool Defer,
    string Reason,
    string Regulation);

/// <summary>Discriminates the outcome of a deletion request.</summary>
public enum RequestDeletionResult
{
    /// <summary>A cooling-off period was started; erasure is scheduled for later.</summary>
    Deferred,

    /// <summary>Deletion executed immediately.</summary>
    ExecutedImmediately,

    /// <summary>A deferred request is already pending for this subject — the new request was rejected.</summary>
    DuplicatePending,
}

/// <summary>Result of <see cref="IPrivacyDeletionRequestService.RequestDeletionAsync"/>.</summary>
/// <param name="Result">What happened.</param>
/// <param name="RequestId">Correlation id of the created request (unset / ignored for <see cref="RequestDeletionResult.DuplicatePending"/>).</param>
/// <param name="ScheduledDeletionAt">Scheduled erasure timestamp for a deferred request; <c>null</c> otherwise.</param>
public sealed record RequestDeletionOutcome(
    RequestDeletionResult Result,
    Guid RequestId,
    DateTimeOffset? ScheduledDeletionAt);

/// <summary>Discriminates the outcome of a deletion-cancellation request.</summary>
public enum CancelDeletionResult
{
    /// <summary>The deferred request was cancelled.</summary>
    Cancelled,

    /// <summary>No request with that id belongs to the caller.</summary>
    NotFound,

    /// <summary>The request exists but is no longer in a cancellable (deferred) state.</summary>
    NotCancellable,
}

/// <summary>Result of <see cref="IPrivacyDeletionRequestService.CancelDeletionAsync"/>.</summary>
/// <param name="Result">What happened.</param>
/// <param name="CurrentState">The request's current state — set only for <see cref="CancelDeletionResult.NotCancellable"/>.</param>
public sealed record CancelDeletionOutcome(
    CancelDeletionResult Result,
    DeletionRequestState? CurrentState = null);
