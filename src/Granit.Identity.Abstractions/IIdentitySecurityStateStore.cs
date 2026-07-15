namespace Granit.Identity;

/// <summary>
/// The outcome recorded when a user reviews one of their own sessions after a step-up challenge.
/// </summary>
public enum UserSessionReviewDecision
{
    /// <summary>The user confirmed the session as theirs.</summary>
    Confirmed,

    /// <summary>The user denied the session (not theirs) — it should be revoked.</summary>
    Denied,
}

/// <summary>
/// Durable store for a user's session-security state: per-session risk verdicts, per-device trust,
/// the habitual behavioural profile, and single-use session-review decisions. All state is keyed by
/// <c>userId</c> and shares one backing store (in-memory by default, EF Core in production).
/// </summary>
/// <remarks>
/// <para>
/// This aggregates what used to be four separate micro-stores. The facets are cohesive — they are the
/// durable state the session surfaces, the anomaly detector, and the step-up review flow all read and
/// write against a single user — and were only ever swapped as a set (in-memory ↔ EF Core), so one
/// interface with one pair of implementations replaces four interfaces with eight.
/// </para>
/// <para>
/// The default registration is a non-durable, single-node in-memory implementation. Install
/// <c>Granit.Identity.EntityFrameworkCore</c> to replace it with a durable, cross-instance store —
/// the in-memory profile and review decisions otherwise reset on restart and do not span pods.
/// </para>
/// </remarks>
public interface IIdentitySecurityStateStore
{
    // ── Session risk ──────────────────────────────────────────────────

    /// <summary>Persists the risk verdict for a session (upsert by user + session).</summary>
    Task SetSessionRiskAsync(
        string userId,
        string sessionId,
        UserSessionRiskVerdict verdict,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the risk verdict for a session, or <c>null</c> if none was recorded.</summary>
    Task<UserSessionRiskVerdict?> GetSessionRiskAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the risk verdicts for several of a user's sessions, keyed by session id.</summary>
    Task<IReadOnlyDictionary<string, UserSessionRiskVerdict>> GetSessionRisksAsync(
        string userId,
        IReadOnlyCollection<string> sessionIds,
        CancellationToken cancellationToken = default);

    // ── Device trust ──────────────────────────────────────────────────

    /// <summary>Persists the trust verdict for a device (upsert by user + device).</summary>
    Task SetDeviceTrustAsync(
        string userId,
        string deviceId,
        DeviceTrustVerdict verdict,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the trust verdict for a device, or <c>null</c> if none was recorded.</summary>
    Task<DeviceTrustVerdict?> GetDeviceTrustAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the trust verdicts for several of a user's devices, keyed by device id.</summary>
    Task<IReadOnlyDictionary<string, DeviceTrustVerdict>> GetDeviceTrustsAsync(
        string userId,
        IReadOnlyCollection<string> deviceIds,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a device's trust (removes the record).</summary>
    Task RevokeDeviceTrustAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default);

    // ── Behavioural profile ───────────────────────────────────────────

    /// <summary>Gets the user's habitual behavioural profile (empty when nothing was recorded yet).</summary>
    Task<UserBehavioralProfile> GetBehavioralProfileAsync(
        string userId, CancellationToken cancellationToken = default);

    /// <summary>Records one observation (country / device family / coarse location) into the profile.</summary>
    Task RecordBehavioralObservationAsync(
        string userId,
        string? country,
        string? deviceFamily,
        string? coarseLocation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);

    // ── Session review (single-use) ───────────────────────────────────

    /// <summary>Gets the review decision for a session, or <c>null</c> if it was never reviewed.</summary>
    Task<UserSessionReviewDecision?> GetSessionReviewDecisionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a session-review decision, returning <c>true</c> only for the first (winning) attempt.
    /// A repeat or concurrent attempt returns <c>false</c> — single-use semantics.
    /// </summary>
    Task<bool> TryRecordSessionReviewDecisionAsync(
        string userId,
        string sessionId,
        UserSessionReviewDecision decision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default);
}
