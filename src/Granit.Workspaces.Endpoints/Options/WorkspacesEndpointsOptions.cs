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
}
