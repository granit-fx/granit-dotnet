namespace Granit.OpenIddict.Services;

/// <summary>
/// Persists and reads the last-activity timestamp of OpenIddict-issued sessions on the refresh token
/// that represents the session.
/// </summary>
/// <remarks>
/// This is the durable, server-side session-activity store for the OpenIddict authority — the local
/// equivalent of the BFF's <c>IBffTokenStore</c> touch and Keycloak's native session last-access. The
/// heartbeat touches it; the session provider reads it into <c>UserSessionDescriptor.LastAccessedAt</c>;
/// the idle-session job enforces on it.
/// </remarks>
public interface IUserSessionActivityStore
{
    /// <summary>
    /// Records activity on the session identified by <paramref name="authorizationId"/> by stamping
    /// <paramref name="lastActivityAt"/> on its refresh token — debounced: a no-op when the stored
    /// activity is newer than <paramref name="debounceWindow"/>, so a chatty heartbeat does not write
    /// on every request.
    /// </summary>
    /// <param name="authorizationId">The authorization shared by the session's access and refresh tokens.</param>
    /// <param name="lastActivityAt">The activity timestamp to record.</param>
    /// <param name="debounceWindow">Skip the write when the stored activity is within this window of now.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TouchAsync(
        Guid authorizationId,
        DateTimeOffset lastActivityAt,
        TimeSpan debounceWindow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the last-activity timestamp per live session (refresh token id) for a user — a single
    /// batched read for the session listing, keyed by session id.
    /// </summary>
    /// <param name="userId">Subject whose sessions' activity to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, DateTimeOffset>> GetActivitiesAsync(
        string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ids of valid refresh tokens whose session has been idle since before
    /// <paramref name="idleSince"/> — using the last-activity timestamp, or the token's creation date
    /// when the session was never touched (a session that never sent a heartbeat is idle from birth).
    /// The idle-session job loads each and revokes it unless it is a remember-me session.
    /// </summary>
    /// <param name="idleSince">The cutoff; sessions with no activity at or after this are idle.</param>
    /// <param name="max">Maximum number of ids to return in one sweep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetIdleRefreshTokenIdsAsync(
        DateTimeOffset idleSince, int max, CancellationToken cancellationToken = default);
}
