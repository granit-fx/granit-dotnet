namespace Granit.Workspaces.Endpoints.Landing;

/// <summary>
/// Closed enum describing which tier of the landing-route precedence
/// resolved the route returned by <c>GET /api/me/landing-route</c>
/// (per ADR-048's 5-tier resolver, story #1556).
/// </summary>
public enum LandingRouteSource
{
    /// <summary>The user's last-visited route, sticky across sessions (highest precedence).</summary>
    PersonalSticky,

    /// <summary>The user's explicitly pinned route (set via <c>PUT /api/me/landing-route/pinned</c>).</summary>
    PersonalPinned,

    /// <summary>Default landing route attached to one of the user's roles.</summary>
    Role,

    /// <summary>Tenant-wide default landing route.</summary>
    Tenant,

    /// <summary>Framework fallback — the configured <c>WorkspacesEndpointsOptions.FrameworkLandingRoute</c>.</summary>
    Framework,
}
