namespace Granit.Identity;

/// <summary>The user's answer to a "was this you?" session-review prompt.</summary>
public enum UserSessionReviewDecision
{
    /// <summary>"Yes, it was me" — the device/location is trusted and the habitual profile reinforced.</summary>
    Confirmed,

    /// <summary>"No, it wasn't me" — sessions are revoked and credential-reset remediation is triggered.</summary>
    Denied,
}

/// <summary>
/// Durable, single-use record of a session review, keyed by <c>(userId, sessionId)</c>. Its sole job is
/// idempotency: a review link may be hit more than once — an email link scanner prefetches it, or the user
/// double-clicks — but the remediation (revoke + credential reset) and the trust/profile reinforcement must run
/// at most once. <see cref="TryRecordDecisionAsync"/> is the atomic check-and-set the endpoint gates on.
/// </summary>
/// <remarks>
/// The default registration (<c>Granit.Identity.Abstractions</c>) is in-memory and non-durable; install
/// <c>Granit.Identity.EntityFrameworkCore</c> for a durable store (otherwise idempotency does not survive a
/// restart or span instances). Depend on it from a scoped/transient service — the in-memory default is
/// <c>Singleton</c>, the EF store <c>Scoped</c>.
/// </remarks>
public interface IUserSessionReviewStore
{
    /// <summary>Reads the recorded decision for a session, or <see langword="null"/> when it has not been reviewed.</summary>
    Task<UserSessionReviewDecision?> GetDecisionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the decision if and only if the session has not been reviewed yet. Returns <see langword="true"/>
    /// when this call performed the first (and only) recording — the caller may then run the side effects — and
    /// <see langword="false"/> when a decision was already recorded (the caller must treat the request as a
    /// no-op). The check-and-set is atomic across concurrent callers.
    /// </summary>
    Task<bool> TryRecordDecisionAsync(
        string userId,
        string sessionId,
        UserSessionReviewDecision decision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default);
}
