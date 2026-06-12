using Granit.Identity.Models;
using Granit.UserSessions;

namespace Granit.Identity.UserSessions.Internal;

/// <summary>
/// <see cref="IUserSessionProvider"/> backed by <see cref="IIdentitySessionManager"/> — the identity
/// provider's server-side SSO sessions. Works for any IdP integration that implements the manager
/// (OpenIddict today). Carries no policy; enrichment and audit live in the session manager.
/// </summary>
internal sealed class IdentityUserSessionProvider(IIdentitySessionManager sessions) : IUserSessionProvider
{
    public async Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId, string? currentSessionId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IdentitySession> idp = await sessions
            .GetUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        return [.. idp.Select(s => new UserSessionDescriptor(
            s.SessionId,
            userId,
            IsCurrent: s.SessionId == currentSessionId,
            s.StartedAt,
            s.LastAccess,
            UserAgent: null,
            s.IpAddress,
            Location: null))];
    }

    public async Task<bool> RevokeAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IdentitySession> idp = await sessions
            .GetUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (!idp.Any(s => s.SessionId == sessionId))
        {
            return false;
        }

        await sessions.TerminateSessionAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> RevokeOthersAsync(
        string userId, string currentSessionId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IdentitySession> idp = await sessions
            .GetUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        int revoked = 0;
        foreach (IdentitySession s in idp)
        {
            if (s.SessionId == currentSessionId)
            {
                continue;
            }

            await sessions.TerminateSessionAsync(userId, s.SessionId, cancellationToken).ConfigureAwait(false);
            revoked++;
        }

        return revoked;
    }
}
