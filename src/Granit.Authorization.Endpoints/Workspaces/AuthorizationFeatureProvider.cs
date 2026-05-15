using Granit.Authorization.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Authorization.Endpoints.Workspaces;

/// <summary>
/// Declares the authorization module's features (per ADR-057). One feature
/// today — the permission definitions browser, gated by
/// <see cref="AuthorizationEndpointsPermissions.Definitions.Read"/>.
/// </summary>
internal sealed class AuthorizationFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(AuthorizationFeatures.Permissions, f => f
            .Permission(AuthorizationEndpointsPermissions.Definitions.Read)
            .RouteName(AuthorizationFeatures.Permissions)
            .DefaultIcon("key")
            .DisplayKey("AuthorizationEndpoints:Workspace.Definitions"));
}
