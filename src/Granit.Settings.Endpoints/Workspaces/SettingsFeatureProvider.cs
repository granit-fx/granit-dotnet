using Granit.Settings.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Settings.Endpoints.Workspaces;

/// <summary>Declares the settings module's features (per ADR-057).</summary>
internal sealed class SettingsFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog)
    {
        catalog.Add(SettingsFeatures.Global, f => f
            .Permission(SettingsPermissions.Global.Read)
            .RouteName(SettingsFeatures.Global)
            .DefaultIcon("settings-2")
            .DisplayKey("SettingsEndpoints:Workspace.Global"));

        catalog.Add(SettingsFeatures.Tenant, f => f
            .Permission(SettingsPermissions.Tenant.Read)
            .RouteName(SettingsFeatures.Tenant)
            .DefaultIcon("building")
            .DisplayKey("SettingsEndpoints:Workspace.Tenant"));
    }
}
