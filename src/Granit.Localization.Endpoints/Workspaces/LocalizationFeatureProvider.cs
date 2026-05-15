using Granit.Localization.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Localization.Endpoints.Workspaces;

/// <summary>Declares the localization-overrides module's features (per ADR-057).</summary>
internal sealed class LocalizationFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(LocalizationFeatures.Overrides, f => f
            .Permission(LocalizationOverridesPermissions.Overrides.Read)
            .RouteName(LocalizationFeatures.Overrides)
            .DefaultIcon("languages")
            .DisplayKey("LocalizationEndpoints:Workspace.Item"));
}
