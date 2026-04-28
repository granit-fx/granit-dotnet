using Granit.Authorization;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Dashboards.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Dashboards.*</c> permissions in the Granit RBAC system.
/// </summary>
internal sealed class DashboardsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            DashboardsPermissions.GroupName,
            LocalizableString.Create<DashboardsEndpointsLocalizationResource>(
                "PermissionGroup:Dashboards"));

        group.AddPermission(
            DashboardsPermissions.Catalog.Read,
            LocalizableString.Create<DashboardsEndpointsLocalizationResource>(
                "Permission:Dashboards.Catalog.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            DashboardsPermissions.Instances.Manage,
            LocalizableString.Create<DashboardsEndpointsLocalizationResource>(
                "Permission:Dashboards.Instances.Manage"),
            MultiTenancySides.Both);
    }
}
