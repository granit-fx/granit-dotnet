using Granit.Bff.Options;
using Granit.Identity;
using Microsoft.Extensions.Options;

namespace Granit.Bff.UserSessions.Internal;

/// <summary>
/// <see cref="IUserSessionProvider"/> and <see cref="IUserDeviceProvider"/> backed by the BFF token store: a
/// user's browser↔BFF sessions (one per device/login on this relying party), aggregated across every configured
/// frontend. Serving both facets from one store keeps the <c>/sessions</c> and <c>/devices</c> views consistent
/// when the BFF wins the session facet by precedence.
/// </summary>
/// <remarks>
/// Carries no policy — enrichment, audit and authorization live in <c>IUserSessionManager</c>. The
/// session id is the BFF session cookie value; "current" is decided by the caller-supplied id.
/// </remarks>
internal sealed class BffUserSessionProvider(
    IBffTokenStore tokenStore,
    IOptions<GranitBffOptions> options) : IUserSessionProvider, IUserDeviceProvider
{
    public async Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId, string? currentSessionId, CancellationToken cancellationToken = default)
    {
        List<UserSessionDescriptor> sessions = [];

        foreach (string frontendName in options.Value.Frontends.Select(f => f.Name))
        {
            IReadOnlyList<string> sessionIds = await tokenStore
                .GetSessionIdsByUserAsync(frontendName, userId, cancellationToken)
                .ConfigureAwait(false);

            foreach (string sessionId in sessionIds)
            {
                BffTokenSet? tokens = await tokenStore
                    .GetAsync(frontendName, sessionId, cancellationToken)
                    .ConfigureAwait(false);
                if (tokens is null)
                {
                    continue;
                }

                sessions.Add(new UserSessionDescriptor(
                    sessionId,
                    tokens.UserId,
                    IsCurrent: sessionId == currentSessionId,
                    tokens.SessionCreatedAt,
                    tokens.LastAccessedAt,
                    tokens.UserAgent,
                    tokens.IpAddress,
                    Location: null));
            }
        }

        return sessions;
    }

    public async Task<IReadOnlyList<UserDevice>> ListAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        // The BFF store exposes no stable device id of its own — synthesize one device per IP from the user's
        // own BFF sessions, so the device list stays consistent with the session list from the same store.
        IReadOnlyList<UserSessionDescriptor> sessions =
            await ListAsync(userId, currentSessionId: null, cancellationToken).ConfigureAwait(false);
        return UserDeviceGrouping.ByIpAddress(sessions);
    }

    public async Task<bool> RevokeAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        foreach (string frontendName in options.Value.Frontends.Select(f => f.Name))
        {
            BffTokenSet? tokens = await tokenStore
                .GetAsync(frontendName, sessionId, cancellationToken)
                .ConfigureAwait(false);

            // Only revoke a session that exists on this frontend AND belongs to the caller —
            // never let one user revoke another's session by guessing an id.
            if (tokens is not null && tokens.UserId == userId)
            {
                await tokenStore.RemoveAsync(frontendName, sessionId, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    public async Task<int> RevokeOthersAsync(
        string userId, string currentSessionId, CancellationToken cancellationToken = default)
    {
        int revoked = 0;

        foreach (string frontendName in options.Value.Frontends.Select(f => f.Name))
        {
            IReadOnlyList<string> sessionIds = await tokenStore
                .GetSessionIdsByUserAsync(frontendName, userId, cancellationToken)
                .ConfigureAwait(false);

            foreach (string sessionId in sessionIds)
            {
                if (sessionId == currentSessionId)
                {
                    continue;
                }

                await tokenStore.RemoveAsync(frontendName, sessionId, cancellationToken).ConfigureAwait(false);
                revoked++;
            }
        }

        return revoked;
    }
}
