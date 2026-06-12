namespace Granit.Identity;

/// <summary>
/// Thin, backend-specific adapter that exposes a user's sessions and the irreducible
/// operations to revoke them. One implementation per session backend — BFF gateway store,
/// OpenIddict authority, Keycloak — registered by the matching integration package.
/// </summary>
/// <remarks>
/// <para>
/// A provider carries <strong>no policy</strong>: it does not authorize the caller, write audit
/// rows, resolve geolocation, or compute risk. Those are centralized in
/// <see cref="IUserSessionManager"/>, which is the single orchestrator that calls into the
/// registered provider. The provider only knows how to talk to its backend.
/// </para>
/// <para>
/// Returned <see cref="UserSessionDescriptor"/> values carry the raw backend data
/// (<see cref="UserSessionDescriptor.IpAddress"/>, timestamps, user-agent) with
/// <see cref="UserSessionDescriptor.Location"/> left <see langword="null"/> — the manager fills
/// geolocation. The provider does set <see cref="UserSessionDescriptor.IsCurrent"/> by comparing
/// against <c>currentSessionId</c>, since only it knows how its backend identifies a session.
/// </para>
/// <para>
/// The default registration is a no-op returning no sessions, so the canonical session API
/// resolves everywhere; install a backend integration package to surface real sessions.
/// </para>
/// </remarks>
public interface IUserSessionProvider
{
    /// <summary>
    /// Lists the backend sessions belonging to <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">Subject whose sessions to list.</param>
    /// <param name="currentSessionId">
    /// The caller's current session id (resolved at the transport layer), used to flag
    /// <see cref="UserSessionDescriptor.IsCurrent"/>; <see langword="null"/> when unknown.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId,
        string? currentSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a single session of <paramref name="userId"/>.
    /// </summary>
    /// <returns><see langword="true"/> when a matching session was found and revoked.</returns>
    Task<bool> RevokeAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every session of <paramref name="userId"/> except <paramref name="currentSessionId"/>.
    /// </summary>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeOthersAsync(
        string userId,
        string currentSessionId,
        CancellationToken cancellationToken = default);
}
