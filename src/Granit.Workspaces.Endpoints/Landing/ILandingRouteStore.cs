namespace Granit.Workspaces.Endpoints.Landing;

/// <summary>
/// Persistence surface for per-user landing-route preferences (sticky + pinned).
/// Hosts that want real persistence ship their own implementation; the
/// framework registers <see cref="NullLandingRouteStore"/> by default so
/// <c>GET /api/me/landing-route</c> falls through to lower tiers.
/// </summary>
public interface ILandingRouteStore
{
    /// <summary>Returns the user's last-visited route, or <see langword="null"/> if none recorded.</summary>
    Task<string?> GetStickyAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's explicitly pinned route, or <see langword="null"/> if not pinned.</summary>
    Task<string?> GetPinnedAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Records or clears the user's pinned route.</summary>
    Task SetPinnedAsync(string userId, Guid? tenantId, string? route, CancellationToken cancellationToken = default);
}

/// <summary>
/// Null-object default — never records anything, always returns <see langword="null"/>.
/// Hosts that want sticky / pinned behaviour register a real implementation
/// over this one through <c>services.AddSingleton&lt;ILandingRouteStore, ...&gt;()</c>.
/// </summary>
internal sealed class NullLandingRouteStore : ILandingRouteStore
{
    public Task<string?> GetStickyAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<string?> GetPinnedAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task SetPinnedAsync(string userId, Guid? tenantId, string? route, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
