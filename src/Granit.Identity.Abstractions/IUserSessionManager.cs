namespace Granit.Identity;

/// <summary>
/// The single, topology-agnostic orchestrator for user-session management. It is the one entry
/// point the canonical <c>/sessions</c> and <c>/devices</c> API depends on, regardless of whether a
/// BFF is present or whether the identity provider is OpenIddict or Keycloak.
/// </summary>
/// <remarks>
/// <para>
/// The manager centralizes all policy: it enriches each session with geolocation and the persisted
/// risk verdict (<see cref="IUserSessionRiskStore"/>) exactly once, and dispatches revoke commands to
/// the registered backend <see cref="IUserSessionProvider"/> / <see cref="IUserDeviceProvider"/>.
/// Backends contribute only the irreducible mechanism (querying and revoking in their store);
/// they never duplicate this enrichment or authorization logic.
/// </para>
/// <para>
/// Authorization (the caller may only manage their own sessions, unless an administrator) and audit
/// are applied by the manager and its hosting endpoints, not by the providers.
/// </para>
/// </remarks>
public interface IUserSessionManager
{
    /// <summary>
    /// Lists <paramref name="userId"/>'s sessions, enriched with geolocation and risk.
    /// </summary>
    /// <param name="userId">Subject whose sessions to list.</param>
    /// <param name="currentSessionId">The caller's current session id, to flag the current entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<UserSessionView>> ListAsync(
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

    /// <summary>
    /// Lists <paramref name="userId"/>'s devices, enriched with geolocation.
    /// </summary>
    Task<IReadOnlyList<UserDevice>> ListDevicesAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
