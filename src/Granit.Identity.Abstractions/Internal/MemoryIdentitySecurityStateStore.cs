using System.Collections.Concurrent;

namespace Granit.Identity.Internal;

/// <summary>
/// In-memory, single-node, non-durable <see cref="IIdentitySecurityStateStore"/> registered by default.
/// Suitable for development and tests; replaced by <c>Granit.Identity.EntityFrameworkCore</c> for durable
/// production use (the in-memory profile and review decisions reset on restart and do not span instances).
/// </summary>
internal sealed class MemoryIdentitySecurityStateStore : IIdentitySecurityStateStore
{
    private readonly ConcurrentDictionary<(string UserId, string SessionId), UserSessionRiskVerdict> _risks = new();
    private readonly ConcurrentDictionary<(string UserId, string DeviceId), DeviceTrustVerdict> _deviceTrusts = new();
    private readonly ConcurrentDictionary<(string UserId, string SessionId), UserSessionReviewDecision> _reviews = new();
    private readonly ConcurrentDictionary<(string UserId, BehavioralObservationKind Kind, string Value), ProfileEntry> _profile = new();
    private readonly Lock _profileGate = new();

    // ── Session risk ──────────────────────────────────────────────────

    public Task SetSessionRiskAsync(
        string userId, string sessionId, UserSessionRiskVerdict verdict, CancellationToken cancellationToken = default)
    {
        _risks[(userId, sessionId)] = verdict;
        return Task.CompletedTask;
    }

    public Task<UserSessionRiskVerdict?> GetSessionRiskAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_risks.GetValueOrDefault((userId, sessionId)));

    public Task<IReadOnlyDictionary<string, UserSessionRiskVerdict>> GetSessionRisksAsync(
        string userId, IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken = default)
    {
        Dictionary<string, UserSessionRiskVerdict> result = [];
        foreach (string sessionId in sessionIds)
        {
            if (_risks.TryGetValue((userId, sessionId), out UserSessionRiskVerdict? verdict))
            {
                result[sessionId] = verdict;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, UserSessionRiskVerdict>>(result);
    }

    // ── Device trust ──────────────────────────────────────────────────

    public Task SetDeviceTrustAsync(
        string userId, string deviceId, DeviceTrustVerdict verdict, CancellationToken cancellationToken = default)
    {
        _deviceTrusts[(userId, deviceId)] = verdict;
        return Task.CompletedTask;
    }

    public Task<DeviceTrustVerdict?> GetDeviceTrustAsync(
        string userId, string deviceId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_deviceTrusts.GetValueOrDefault((userId, deviceId)));

    public Task<IReadOnlyDictionary<string, DeviceTrustVerdict>> GetDeviceTrustsAsync(
        string userId, IReadOnlyCollection<string> deviceIds, CancellationToken cancellationToken = default)
    {
        Dictionary<string, DeviceTrustVerdict> result = [];
        foreach (string deviceId in deviceIds)
        {
            if (_deviceTrusts.TryGetValue((userId, deviceId), out DeviceTrustVerdict? verdict))
            {
                result[deviceId] = verdict;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, DeviceTrustVerdict>>(result);
    }

    public Task RevokeDeviceTrustAsync(
        string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        _deviceTrusts.TryRemove((userId, deviceId), out _);
        return Task.CompletedTask;
    }

    // ── Behavioural profile ───────────────────────────────────────────

    public Task<UserBehavioralProfile> GetBehavioralProfileAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        List<BehavioralObservation> observations;
        lock (_profileGate)
        {
            observations =
            [
                .. _profile
                    .Where(kvp => kvp.Key.UserId == userId)
                    .Select(kvp => new BehavioralObservation(
                        kvp.Key.Kind, kvp.Key.Value, kvp.Value.Count, kvp.Value.FirstSeenAt, kvp.Value.LastSeenAt)),
            ];
        }

        return Task.FromResult(
            observations.Count == 0 ? UserBehavioralProfile.Empty : new UserBehavioralProfile(observations));
    }

    public Task RecordBehavioralObservationAsync(
        string userId,
        string? country,
        string? deviceFamily,
        string? coarseLocation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        lock (_profileGate)
        {
            RecordObservation(userId, BehavioralObservationKind.Country, country, observedAt);
            RecordObservation(userId, BehavioralObservationKind.DeviceFamily, deviceFamily, observedAt);
            RecordObservation(userId, BehavioralObservationKind.CoarseLocation, coarseLocation, observedAt);
        }

        return Task.CompletedTask;
    }

    private void RecordObservation(
        string userId, BehavioralObservationKind kind, string? value, DateTimeOffset observedAt)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (_profile.TryGetValue((userId, kind, value), out ProfileEntry? entry))
        {
            entry.Count++;
            entry.LastSeenAt = observedAt;
        }
        else
        {
            _profile[(userId, kind, value)] =
                new ProfileEntry { Count = 1, FirstSeenAt = observedAt, LastSeenAt = observedAt };
        }
    }

    // ── Session review (single-use) ───────────────────────────────────

    public Task<UserSessionReviewDecision?> GetSessionReviewDecisionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<UserSessionReviewDecision?>(
            _reviews.TryGetValue((userId, sessionId), out UserSessionReviewDecision decision) ? decision : null);

    public Task<bool> TryRecordSessionReviewDecisionAsync(
        string userId,
        string sessionId,
        UserSessionReviewDecision decision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default) =>
        // TryAdd is atomic: exactly one concurrent caller wins, giving single-use semantics for free.
        Task.FromResult(_reviews.TryAdd((userId, sessionId), decision));

    private sealed class ProfileEntry
    {
        public int Count { get; set; }
        public DateTimeOffset FirstSeenAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
    }
}
