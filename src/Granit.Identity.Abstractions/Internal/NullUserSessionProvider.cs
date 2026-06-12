namespace Granit.Identity.Internal;

/// <summary>
/// No-op <see cref="IUserSessionProvider"/> registered by default: reports no sessions and revokes
/// nothing. Replaced when a backend integration package (BFF, OpenIddict, Keycloak) is installed, so
/// the canonical session API always resolves but only surfaces real data once a backend is wired.
/// </summary>
internal sealed class NullUserSessionProvider : IUserSessionProvider
{
    public Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId, string? currentSessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserSessionDescriptor>>([]);

    public Task<bool> RevokeAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<int> RevokeOthersAsync(
        string userId, string currentSessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
