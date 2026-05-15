using Granit.Authentication.ApiKeys.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Authentication.ApiKeys.Endpoints.Workspaces;

/// <summary>
/// Declares the API-keys module's features for the host's workspace
/// composition (per ADR-057). One feature: the API-keys management
/// dashboard, gated by <see cref="ApiKeyPermissions.Keys.Read"/>.
/// </summary>
internal sealed class ApiKeysFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(ApiKeysFeatures.Keys, f => f
            .Permission(ApiKeyPermissions.Keys.Read)
            .RouteName(ApiKeysFeatures.Keys)
            .DefaultIcon("key-round")
            .DisplayKey("AuthenticationApiKeysEndpoints:Workspace.Item"));
}
