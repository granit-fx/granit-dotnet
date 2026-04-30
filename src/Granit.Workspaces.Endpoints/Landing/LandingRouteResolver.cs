using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Workspaces.Endpoints.Options;
using Microsoft.Extensions.Options;

namespace Granit.Workspaces.Endpoints.Landing;

/// <summary>
/// Resolves the user's effective landing route per ADR-048's 5-tier
/// precedence (story #1556):
/// <c>personal-sticky &gt; personal-pinned &gt; role &gt; tenant &gt; framework</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each tier may return <see langword="null"/> (provider not configured, no
/// preference recorded), in which case the resolver falls through to the
/// next tier. URL whitelist validation runs against
/// <see cref="WorkspacesEndpointsOptions.LandingRouteAllowedPrefixes"/> at
/// every tier; a route that fails validation is treated as if the tier had
/// returned <see langword="null"/> — the resolver falls through. The
/// framework fallback is configured (not user-supplied), so it is trusted
/// and not re-validated.
/// </para>
/// <para>
/// Permission fallthrough on the workspace target is the host's responsibility
/// — the resolver itself is permission-agnostic. Hosts that want
/// "drop the route if the user can't access the referenced workspace" wire a
/// custom <see cref="ILandingRouteAccessGuard"/> implementation; the default
/// guard accepts every route.
/// </para>
/// </remarks>
public sealed class LandingRouteResolver(
    ILandingRouteStore store,
    IRoleLandingRouteProvider roleProvider,
    ITenantLandingRouteProvider tenantProvider,
    ILandingRouteAccessGuard accessGuard,
    IOptions<WorkspacesEndpointsOptions> options,
    ICurrentTenant? currentTenant = null)
{
    /// <summary>
    /// Resolves the route for the requesting user. Always returns a result —
    /// the framework fallback closes the precedence chain.
    /// </summary>
    public async Task<LandingRouteResult> ResolveAsync(
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        string? userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        Guid? tenantId = currentTenant is { IsAvailable: true } ct ? ct.Id : null;

        if (userId is not null)
        {
            string? sticky = await store.GetStickyAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            if (await IsAcceptableAsync(sticky, user, cancellationToken).ConfigureAwait(false))
            {
                return new LandingRouteResult(sticky!, LandingRouteSource.PersonalSticky);
            }

            string? pinned = await store.GetPinnedAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            if (await IsAcceptableAsync(pinned, user, cancellationToken).ConfigureAwait(false))
            {
                return new LandingRouteResult(pinned!, LandingRouteSource.PersonalPinned);
            }
        }

        string? roleDefault = await roleProvider.GetRoleDefaultAsync(user, cancellationToken).ConfigureAwait(false);
        if (await IsAcceptableAsync(roleDefault, user, cancellationToken).ConfigureAwait(false))
        {
            return new LandingRouteResult(roleDefault!, LandingRouteSource.Role);
        }

        string? tenantDefault = await tenantProvider.GetTenantDefaultAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (await IsAcceptableAsync(tenantDefault, user, cancellationToken).ConfigureAwait(false))
        {
            return new LandingRouteResult(tenantDefault!, LandingRouteSource.Tenant);
        }

        return new LandingRouteResult(options.Value.FrameworkLandingRoute, LandingRouteSource.Framework);
    }

    /// <summary>Whitelist validator — public so the PUT endpoint can reuse it before persisting a pinned route.</summary>
    public bool IsAllowedRoute(string? route) =>
        IsAllowedRoute(route, options.Value);

    internal static bool IsAllowedRoute(string? route, WorkspacesEndpointsOptions opts)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        foreach (string prefix in opts.LandingRouteAllowedPrefixes)
        {
            if (route.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<bool> IsAcceptableAsync(string? route, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!IsAllowedRoute(route, options.Value))
        {
            return false;
        }
        return await accessGuard.CanAccessAsync(route!, user, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>The tier the resolver picked.</summary>
/// <param name="Route">The resolved route.</param>
/// <param name="Source">Which tier of the precedence chain produced the route.</param>
public sealed record LandingRouteResult(string Route, LandingRouteSource Source);

/// <summary>
/// Permission-aware guard that lets hosts veto a route the resolver would
/// otherwise return — the guard says "this user cannot access /w/Granit.Framework",
/// the resolver falls through to the next tier. Default implementation
/// (<see cref="AllowAllLandingRouteAccessGuard"/>) accepts every route.
/// </summary>
public interface ILandingRouteAccessGuard
{
    /// <summary>Returns <see langword="true"/> when the user is allowed to land on <paramref name="route"/>.</summary>
    Task<bool> CanAccessAsync(string route, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>Null-object — accepts every route.</summary>
internal sealed class AllowAllLandingRouteAccessGuard : ILandingRouteAccessGuard
{
    public Task<bool> CanAccessAsync(string route, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
