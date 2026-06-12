namespace Granit.UserSessions.AnomalyDetection;

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
    Task<UserSessionRiskAssessment> EvaluateAsync(
        UserSessionDescriptor candidate,
        IReadOnlyList<UserSessionDescriptor> history,
        CancellationToken cancellationToken = default);
}
