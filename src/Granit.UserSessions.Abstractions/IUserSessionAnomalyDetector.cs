namespace Granit.UserSessions;

/// <summary>
/// Evaluates a candidate session against the user's session history to detect anomalies.
/// </summary>
/// <remarks>
/// The default registration is a no-op returning <see cref="UserSessionRiskAssessment.None"/>. Install
/// <c>Granit.UserSessions.AnomalyDetection</c> to enable heuristic (and optionally AI-assisted) detection.
/// Implementations must never throw for evaluation failures — degrade to a lower-confidence verdict instead.
/// </remarks>
public interface IUserSessionAnomalyDetector
{
    /// <summary>
    /// Assesses <paramref name="candidate"/> in the context of <paramref name="history"/>.
    /// </summary>
    /// <param name="candidate">The session being evaluated (typically a newly established one).</param>
    /// <param name="history">The user's other known sessions, most-recent first.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The risk assessment.</returns>
    Task<UserSessionRiskAssessment> AssessAsync(
        UserSessionDescriptor candidate,
        IReadOnlyList<UserSessionDescriptor> history,
        CancellationToken cancellationToken = default);
}
