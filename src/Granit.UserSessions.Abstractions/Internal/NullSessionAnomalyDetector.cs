namespace Granit.UserSessions.Internal;

/// <summary>
/// No-op <see cref="ISessionAnomalyDetector"/> registered by default: every session is
/// <see cref="SessionRiskAssessment.None"/>. Replaced when <c>Granit.UserSessions.AnomalyDetection</c> is installed.
/// </summary>
internal sealed class NullSessionAnomalyDetector : ISessionAnomalyDetector
{
    public Task<SessionRiskAssessment> AssessAsync(
        SessionDescriptor candidate,
        IReadOnlyList<SessionDescriptor> history,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SessionRiskAssessment.None);
}
