using Granit.Events;
using Granit.Identity.AnomalyDetection.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Options;

namespace Granit.Identity.AnomalyDetection.Internal;

/// <summary>
/// Default <see cref="IUserSessionRiskEvaluator"/>. Persists a non-<c>None</c> verdict to the durable store so
/// it shows up on the session surfaces, and raises <see cref="SuspiciousUserSessionDetectedEto"/> for
/// Medium/High risk when a distributed event bus is available.
/// </summary>
/// <remarks>
/// The persisted verdict is the durable source of truth (the session surfaces read it back); the
/// <see cref="SuspiciousUserSessionDetectedEto"/> is a <strong>best-effort reactive signal</strong>. The two
/// writes are intentionally decoupled — the verdict commits in the risk store's own transaction, then the Eto
/// publishes — because the store sits behind an abstraction that may not be transactional (the in-memory
/// default) and the evaluator carries no ambient DbContext to share an outbox transaction with. A crash between
/// the two therefore loses the Eto, not the verdict: a consumer that needs guaranteed delivery should reconcile
/// against the durable store rather than rely solely on the event.
/// </remarks>
internal sealed class DefaultUserSessionRiskEvaluator(
    IUserSessionAnomalyDetector detector,
    IUserSessionRiskStore riskStore,
    TimeProvider timeProvider,
    ICurrentTenant currentTenant,
    IOptions<UserSessionsAnomalyDetectionOptions> options,
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

            // Best-effort, non-atomic with the store write above — see the class remarks for the rationale.
            if (assessment.Level >= UserSessionRiskLevel.Medium && eventBus is not null)
            {
                await eventBus
                    .PublishAsync(
                        new SuspiciousUserSessionDetectedEto(
                            userId,
                            candidate.SessionId,
                            currentTenant.IsAvailable ? currentTenant.Id : null,
                            assessment.Level,
                            assessment.Reasons,
                            assessment.Score,
                            candidate.Location?.City,
                            candidate.Location?.CountryCode,
                            candidate.UserAgent,
                            // Raw IP withheld by default (GDPR); coarse location carried instead. A deployment
                            // can opt into raw-IP exposure on the alert via IncludeClientIpInAlert.
                            options.Value.IncludeClientIpInAlert ? candidate.IpAddress : null,
                            now),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return assessment;
    }
}
