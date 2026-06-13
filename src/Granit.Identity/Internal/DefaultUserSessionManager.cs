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
    IUserSessionRiskStore riskStore) : IUserSessionManager
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

    public Task<IReadOnlyList<UserDevice>> ListDevicesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        return deviceProvider.ListAsync(userId, cancellationToken);
    }
}
