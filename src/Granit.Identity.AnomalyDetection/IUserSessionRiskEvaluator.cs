namespace Granit.Identity.AnomalyDetection;

/// <summary>
/// Orchestrates session risk evaluation: runs the anomaly detector, persists a non-<c>None</c> verdict to the
/// <see cref="IUserSessionRiskStore"/>, and raises a <c>SuspiciousUserSessionDetectedEto</c> for Medium/High
/// risk. Consumers (the BFF login flow, the identity authority) call this when a session is established.
/// </summary>
public interface IUserSessionRiskEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="candidate"/> against <paramref name="history"/>, records the verdict, and
    /// returns the assessment.
    /// </summary>
    /// <param name="candidate">The newly established session under assessment.</param>
    /// <param name="history">The user's other (geo-enriched) sessions to compare against.</param>
    /// <param name="deviceId">
    /// The stable device id the session was established from, when resolved at the HTTP boundary. When the device
    /// is actively trusted, a notable (Medium) anomaly is downgraded to informational (Low) — a device the user
    /// explicitly trusts is less likely to be a surprise. A strong (High) anomaly is preserved: a trusted device
    /// can still be compromised (e.g. stolen token + impossible travel).
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<UserSessionRiskAssessment> EvaluateAsync(
        UserSessionDescriptor candidate,
        IReadOnlyList<UserSessionDescriptor> history,
        string? deviceId = null,
        CancellationToken cancellationToken = default);
}
