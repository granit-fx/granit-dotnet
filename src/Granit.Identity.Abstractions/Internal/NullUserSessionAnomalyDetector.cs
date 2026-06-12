namespace Granit.Identity.Internal;

/// <summary>
/// No-op <see cref="IUserSessionAnomalyDetector"/> registered by default: every session is
/// <see cref="UserSessionRiskAssessment.None"/>. Replaced when <c>Granit.Identity.AnomalyDetection</c> is installed.
/// </summary>
internal sealed class NullUserSessionAnomalyDetector : IUserSessionAnomalyDetector
{
    public Task<UserSessionRiskAssessment> AssessAsync(
        UserSessionDescriptor candidate,
        IReadOnlyList<UserSessionDescriptor> history,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(UserSessionRiskAssessment.None);
}
