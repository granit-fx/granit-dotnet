using Granit.Hostnames.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Hostnames.Endpoints.Workspaces;

/// <summary>Declares the managed-hostnames module's features (per ADR-057).</summary>
internal sealed class HostnamesFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(HostnamesFeatures.ManagedHostnames, f => f
            .Permission(HostnamesPermissions.Hostnames.Read)
            .RouteName(HostnamesFeatures.ManagedHostnames)
            .DefaultIcon("globe")
            .DisplayKey("HostnamesEndpoints:Workspace.Item"));
}
