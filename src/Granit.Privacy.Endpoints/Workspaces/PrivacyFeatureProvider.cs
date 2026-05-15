using Granit.Privacy.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Privacy.Endpoints.Workspaces;

/// <summary>Declares the privacy module's features (per ADR-057).</summary>
internal sealed class PrivacyFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog)
    {
        catalog.Add(PrivacyFeatures.Purposes, f => f
            .Permission(PrivacyPermissions.Purposes.Read)
            .RouteName(PrivacyFeatures.Purposes)
            .DefaultIcon("shield-check")
            .DisplayKey("PrivacyEndpoints:Workspace.Purposes"));

        catalog.Add(PrivacyFeatures.Agreements, f => f
            .Permission(PrivacyPermissions.Agreements.Read)
            .RouteName(PrivacyFeatures.Agreements)
            .DefaultIcon("file-signature")
            .DisplayKey("PrivacyEndpoints:Workspace.Agreements"));
    }
}
