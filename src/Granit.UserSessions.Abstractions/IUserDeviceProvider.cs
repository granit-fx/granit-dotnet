namespace Granit.UserSessions;

/// <summary>
/// Thin, backend-specific adapter that lists the devices a user has signed in from. One
/// implementation per session backend, registered by the matching integration package.
/// </summary>
/// <remarks>
/// Like <see cref="IUserSessionProvider"/>, a provider carries no policy — authorization, audit and
/// geolocation enrichment are centralized in <see cref="IUserSessionManager"/>. The default
/// registration returns no devices so the canonical <c>/devices</c> API resolves everywhere.
/// </remarks>
public interface IUserDeviceProvider
{
    /// <summary>
    /// Lists the devices associated with <paramref name="userId"/>'s sessions.
    /// </summary>
    Task<IReadOnlyList<UserDevice>> ListAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
