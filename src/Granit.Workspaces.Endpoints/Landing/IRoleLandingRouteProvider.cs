using System.Security.Claims;

namespace Granit.Workspaces.Endpoints.Landing;

/// <summary>
/// Provider for the role-default tier of the landing-route resolver
/// (per ADR-048, story #1556). When the user belongs to multiple roles with
/// distinct defaults, the provider returns the first match — order is the
/// host's responsibility.
/// </summary>
public interface IRoleLandingRouteProvider
{
    /// <summary>Returns the role-default route, or <see langword="null"/> when no role has one configured.</summary>
    Task<string?> GetRoleDefaultAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>Provider for the tenant-default tier.</summary>
public interface ITenantLandingRouteProvider
{
    /// <summary>Returns the tenant-default route, or <see langword="null"/> when none is configured.</summary>
    Task<string?> GetTenantDefaultAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}

/// <summary>Null-object — always returns <see langword="null"/>, falling through to the next tier.</summary>
internal sealed class NullRoleLandingRouteProvider : IRoleLandingRouteProvider
{
    public Task<string?> GetRoleDefaultAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}

/// <summary>Null-object — always returns <see langword="null"/>, falling through to the next tier.</summary>
internal sealed class NullTenantLandingRouteProvider : ITenantLandingRouteProvider
{
    public Task<string?> GetTenantDefaultAsync(Guid? tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
