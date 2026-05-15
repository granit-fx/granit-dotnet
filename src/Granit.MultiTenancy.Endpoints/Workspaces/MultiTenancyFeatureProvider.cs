using Granit.MultiTenancy.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.MultiTenancy.Endpoints.Workspaces;

/// <summary>Declares the multi-tenancy module's features (per ADR-057).</summary>
internal sealed class MultiTenancyFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(MultiTenancyFeatures.Tenants, f => f
            .Permission(MultiTenancyPermissions.Tenants.Read)
            .RouteName(MultiTenancyFeatures.Tenants)
            .DefaultIcon("building-2")
            .DisplayKey("MultiTenancyEndpoints:Workspace.Tenants"));
}
