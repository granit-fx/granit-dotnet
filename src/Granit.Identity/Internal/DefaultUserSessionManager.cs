namespace Granit.Identity.Internal;

/// <summary>
/// Default <see cref="IUserSessionManager"/>: the single orchestrator behind the canonical session API.
/// It lists sessions through the registered backend <see cref="IUserSessionProvider"/>, attaches the
/// persisted risk verdict from <see cref="IUserSessionRiskStore"/>, and dispatches revoke commands back
/// to the provider. Geolocation enrichment is applied at session creation (stored on the descriptor),
/// not recomputed here.
/// </summary>
internal sealed class DefaultUserSessionManager(
    IUserSessionProvider sessionProvider,
    IUserDeviceProvider deviceProvider,
    IUserSessionRiskStore riskStore,
    IDeviceTrustStore deviceTrustStore,
    TimeProvider timeProvider) : IUserSessionManager
{
    public async Task<IReadOnlyList<UserSessionView>> ListAsync(
        string userId, string? currentSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        IReadOnlyList<UserSessionDescriptor> sessions =
            await sessionProvider.ListAsync(userId, currentSessionId, cancellationToken).ConfigureAwait(false);
        if (sessions.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<string, UserSessionRiskVerdict> verdicts =
            await riskStore.GetManyAsync(userId, [.. sessions.Select(s => s.SessionId)], cancellationToken)
                .ConfigureAwait(false);

        List<UserSessionView> views = new(sessions.Count);
        foreach (UserSessionDescriptor session in sessions)
        {
            verdicts.TryGetValue(session.SessionId, out UserSessionRiskVerdict? verdict);
            views.Add(new UserSessionView(session, verdict));
        }

        return views;
    }

    public Task<bool> RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        return sessionProvider.RevokeAsync(userId, sessionId, cancellationToken);
    }

    public Task<int> RevokeOthersAsync(
        string userId, string currentSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(currentSessionId);
        return sessionProvider.RevokeOthersAsync(userId, currentSessionId, cancellationToken);
    }

    public async Task<int> RevokeAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        // No "current" session to spare (admin acting on another subject): list, then revoke each.
        // The per-session path is uniform across backends; bulk optimisation, where a backend has one,
        // can be added to its provider later without changing this contract.
        IReadOnlyList<UserSessionDescriptor> sessions =
            await sessionProvider.ListAsync(userId, currentSessionId: null, cancellationToken).ConfigureAwait(false);

        int revoked = 0;
        foreach (UserSessionDescriptor session in sessions)
        {
            if (await sessionProvider.RevokeAsync(userId, session.SessionId, cancellationToken).ConfigureAwait(false))
            {
                revoked++;
            }
        }

        return revoked;
    }

    public async Task<IReadOnlyList<UserDevice>> ListDevicesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        IReadOnlyList<UserDevice> devices =
            await deviceProvider.ListAsync(userId, cancellationToken).ConfigureAwait(false);
        if (devices.Count == 0)
        {
            return devices;
        }

        // Attach device-trust state from the store (best-effort: surfaces on devices whose backend id matches a
        // trust key). The authoritative trust path for the current browser is the signed device cookie, consulted
        // at the step-up decision — not list reconciliation.
        IReadOnlyDictionary<string, DeviceTrustVerdict> trust = await deviceTrustStore
            .GetManyAsync(userId, [.. devices.Select(d => d.DeviceId)], cancellationToken)
            .ConfigureAwait(false);
        if (trust.Count == 0)
        {
            return devices;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        return
        [
            .. devices.Select(d =>
                trust.TryGetValue(d.DeviceId, out DeviceTrustVerdict? verdict) && verdict.IsActive(now)
                    ? d with { IsTrusted = true, TrustedUntil = verdict.TrustedUntil }
                    : d),
        ];
    }
}
