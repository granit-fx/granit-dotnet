using Granit.Events;
using Granit.UserSessions.AnomalyDetection.Events;

namespace Granit.UserSessions.AnomalyDetection.Internal;

/// <summary>
/// Default <see cref="IUserSessionRiskEvaluator"/>. Persists a non-<c>None</c> verdict to the durable store so
/// it shows up on the session surfaces, and raises <see cref="SuspiciousUserSessionDetectedEto"/> for
/// Medium/High risk when a distributed event bus is available.
/// </summary>
internal sealed class DefaultUserSessionRiskEvaluator(
    IUserSessionAnomalyDetector detector,
    IUserSessionRiskStore riskStore,
    TimeProvider timeProvider,
    IDistributedEventBus? eventBus = null) : IUserSessionRiskEvaluator
{
    public async Task<UserSessionRiskAssessment> EvaluateAsync(
        UserSessionDescriptor candidate,
        IReadOnlyList<UserSessionDescriptor> history,
        CancellationToken cancellationToken = default)
    {
        UserSessionRiskAssessment assessment = await detector
            .AssessAsync(candidate, history, cancellationToken)
            .ConfigureAwait(false);

        if (candidate.UserId is { } userId && assessment.Level != UserSessionRiskLevel.None)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            await riskStore
                .SetAsync(userId, candidate.SessionId, new UserSessionRiskVerdict(assessment.Level, assessment.Reasons, now), cancellationToken)
                .ConfigureAwait(false);

            if (assessment.Level >= UserSessionRiskLevel.Medium && eventBus is not null)
            {
                string category = assessment.Reasons.Count > 0 ? assessment.Reasons[0] : "anomaly";
                await eventBus
                    .PublishAsync(
                        new SuspiciousUserSessionDetectedEto(userId, candidate.SessionId, category, assessment.Score, now),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return assessment;
    }
}
