namespace Granit.Workspaces.Endpoints.Options;

/// <summary>
/// Configuration for the workspace tree endpoint exposed by
/// <c>MapGranitWorkspacesEndpoints</c>.
/// </summary>
public sealed class WorkspacesEndpointsOptions
{
    /// <summary>OpenAPI Scalar tag (Title Case). Default <c>"Workspaces"</c>.</summary>
    public string TagName { get; set; } = "Workspaces";

    /// <summary>FusionCache TTL for the filtered tree. Default 5 minutes.</summary>
    public TimeSpan TreeCacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Framework fallback returned by <c>GET /api/me/landing-route</c> when no
    /// higher tier resolves (per ADR-048, story #1556). Default <c>"/w/Granit.Framework"</c>.
    /// </summary>
    public string FrameworkLandingRoute { get; set; } = "/w/Granit.Framework";

    /// <summary>
    /// Allowed prefixes for landing routes — the resolver rejects any route
    /// that does not start with one of these. Defaults to <c>"/w/"</c>
    /// (workspace) and <c>"/e/"</c> (entity). Hosts that expose custom
    /// front-end routes append their own prefix here.
    /// </summary>
    public IList<string> LandingRouteAllowedPrefixes { get; set; } = ["/w/", "/e/"];
}
