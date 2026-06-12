using Granit.Bff.Options;
using Granit.Identity;
using Microsoft.Extensions.Options;

namespace Granit.Bff.UserSessions.Internal;

/// <summary>
/// <see cref="IUserSessionProvider"/> backed by the BFF token store: a user's browser↔BFF sessions
/// (one per device/login on this relying party), aggregated across every configured frontend.
/// </summary>
/// <remarks>
/// Carries no policy — enrichment, audit and authorization live in <c>IUserSessionManager</c>. The
/// session id is the BFF session cookie value; "current" is decided by the caller-supplied id.
/// </remarks>
internal sealed class BffUserSessionProvider(
    IBffTokenStore tokenStore,
    IOptions<GranitBffOptions> options) : IUserSessionProvider
{
    public async Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId, string? currentSessionId, CancellationToken cancellationToken = default)
    {
        List<UserSessionDescriptor> sessions = [];

        foreach (BffFrontendOptions frontend in options.Value.Frontends)
        {
            IReadOnlyList<string> sessionIds = await tokenStore
                .GetSessionIdsByUserAsync(frontend.Name, userId, cancellationToken)
                .ConfigureAwait(false);

            foreach (string sessionId in sessionIds)
            {
                BffTokenSet? tokens = await tokenStore
                    .GetAsync(frontend.Name, sessionId, cancellationToken)
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

    public async Task<bool> RevokeAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        foreach (BffFrontendOptions frontend in options.Value.Frontends)
        {
            BffTokenSet? tokens = await tokenStore
                .GetAsync(frontend.Name, sessionId, cancellationToken)
                .ConfigureAwait(false);

            // Only revoke a session that exists on this frontend AND belongs to the caller —
            // never let one user revoke another's session by guessing an id.
            if (tokens is not null && tokens.UserId == userId)
            {
                await tokenStore.RemoveAsync(frontend.Name, sessionId, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    public async Task<int> RevokeOthersAsync(
        string userId, string currentSessionId, CancellationToken cancellationToken = default)
    {
        int revoked = 0;

        foreach (BffFrontendOptions frontend in options.Value.Frontends)
        {
            IReadOnlyList<string> sessionIds = await tokenStore
                .GetSessionIdsByUserAsync(frontend.Name, userId, cancellationToken)
                .ConfigureAwait(false);

            foreach (string sessionId in sessionIds)
            {
                if (sessionId == currentSessionId)
                {
                    continue;
                }

                await tokenStore.RemoveAsync(frontend.Name, sessionId, cancellationToken).ConfigureAwait(false);
                revoked++;
            }
        }

        return revoked;
    }
}
