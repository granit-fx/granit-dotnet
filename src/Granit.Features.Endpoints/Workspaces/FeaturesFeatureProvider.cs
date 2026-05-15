using Granit.Features.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Features.Endpoints.Workspaces;

/// <summary>Declares the feature-flags module's features (per ADR-057).</summary>
internal sealed class FeaturesFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(FeaturesFeatures.Flags, f => f
            .Permission(FeaturesPermissions.Flags.Read)
            .RouteName(FeaturesFeatures.Flags)
            .DefaultIcon("flag")
            .DisplayKey("FeaturesEndpoints:Workspace.Item"));
}
