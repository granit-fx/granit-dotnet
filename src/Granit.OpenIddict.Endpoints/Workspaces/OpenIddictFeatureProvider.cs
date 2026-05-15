using Granit.OpenIddict.Permissions;
using Granit.Workspaces;

namespace Granit.OpenIddict.Endpoints.Workspaces;

/// <summary>
/// Declares the OpenIddict (OIDC) module's features (per ADR-057). Three
/// features — applications, scopes, authorizations — each gated by its
/// existing <see cref="OpenIddictPermissions"/> entry.
/// </summary>
internal sealed class OpenIddictFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog)
    {
        catalog.Add(OpenIddictFeatures.Applications, f => f
            .Permission(OpenIddictPermissions.Applications.Read)
            .RouteName(OpenIddictFeatures.Applications)
            .DefaultIcon("app-window")
            .DisplayKey("OpenIddictEndpoints:Workspace.Applications"));

        catalog.Add(OpenIddictFeatures.Scopes, f => f
            .Permission(OpenIddictPermissions.Scopes.Read)
            .RouteName(OpenIddictFeatures.Scopes)
            .DefaultIcon("scan")
            .DisplayKey("OpenIddictEndpoints:Workspace.Scopes"));

        catalog.Add(OpenIddictFeatures.Authorizations, f => f
            .Permission(OpenIddictPermissions.Authorizations.Read)
            .RouteName(OpenIddictFeatures.Authorizations)
            .DefaultIcon("badge-check")
            .DisplayKey("OpenIddictEndpoints:Workspace.Authorizations"));
    }
}
