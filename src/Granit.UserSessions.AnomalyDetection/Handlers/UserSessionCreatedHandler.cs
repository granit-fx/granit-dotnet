using Granit.IpGeolocation;
using Granit.MultiTenancy;

namespace Granit.UserSessions.AnomalyDetection.Handlers;

/// <summary>
/// Consumes <see cref="UserSessionCreatedEto"/> and runs anomaly detection for the new session, off the login
/// critical path. This is the decoupled trigger that closes the otherwise-unwired
/// <see cref="IUserSessionRiskEvaluator"/>: it resolves geolocation, builds the candidate plus the user's
/// (geo-enriched) session history, then delegates to the evaluator, which persists the verdict and raises
/// <see cref="SuspiciousUserSessionDetectedEto"/> for Medium/High risk.
/// </summary>
public class UserSessionCreatedHandler
{
    /// <summary>Handles a session-created event by assessing the new session's risk.</summary>
    public static async Task HandleAsync(
        UserSessionCreatedEto evt,
        ICurrentTenant currentTenant,
        IUserSessionRiskEvaluator evaluator,
        IUserSessionProvider sessionProvider,
        IIpGeolocationResolver geoResolver,
        CancellationToken cancellationToken)
    {
        // Distributed dispatch carries no ambient tenant — establish it from the event so the risk store, the
        // tenant query filter, and the published Eto are all scoped to the right tenant.
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : evt.TenantId;
        using IDisposable _ = currentTenant.Change(tenantId);

        GeoLocation? candidateLocation = await geoResolver.ResolveAsync(evt.IpAddress, cancellationToken)
            .ConfigureAwait(false);

        UserSessionDescriptor candidate = new(
            evt.SessionId,
            evt.UserId,
            IsCurrent: true,
            evt.CreatedAt,
            LastAccessedAt: null,
            evt.UserAgent,
            evt.IpAddress,
            candidateLocation);

        IReadOnlyList<UserSessionDescriptor> history = await BuildEnrichedHistoryAsync(
            evt, sessionProvider, geoResolver, cancellationToken).ConfigureAwait(false);

        await evaluator.EvaluateAsync(candidate, history, cancellationToken).ConfigureAwait(false);
    }

    // The provider returns the user's other sessions with raw IPs but no resolved location; resolve each here so
    // the heuristics (impossible travel, new country) can compare against history. Lookups are cache-backed and
    // coalesced, so repeated IPs are cheap.
    private static async Task<IReadOnlyList<UserSessionDescriptor>> BuildEnrichedHistoryAsync(
        UserSessionCreatedEto evt,
        IUserSessionProvider sessionProvider,
        IIpGeolocationResolver geoResolver,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UserSessionDescriptor> sessions = await sessionProvider
            .ListAsync(evt.UserId, evt.SessionId, cancellationToken)
            .ConfigureAwait(false);

        List<UserSessionDescriptor> history = [];
        foreach (UserSessionDescriptor session in sessions)
        {
            if (session.SessionId == evt.SessionId)
            {
                continue;
            }

            GeoLocation? location = session.Location
                ?? await geoResolver.ResolveAsync(session.IpAddress, cancellationToken).ConfigureAwait(false);
            history.Add(session with { Location = location });
        }

        return history;
    }
}
