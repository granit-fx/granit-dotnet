using Granit.Activities.Endpoints.Internal;
using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Activities.Endpoints.Permissions;

/// <summary>
/// Declares all Activities permissions in the Granit RBAC system.
/// </summary>
internal sealed class ActivitiesPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            ActivitiesPermissions.GroupName,
            LocalizableString.Create<ActivitiesEndpointsLocalizationResource>(
                "PermissionGroup:Activities"));

        group.AddPermission(
            ActivitiesPermissions.Activities.Read,
            LocalizableString.Create<ActivitiesEndpointsLocalizationResource>(
                "Permission:Activities.Activities.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            ActivitiesPermissions.Activities.ReadOthers,
            LocalizableString.Create<ActivitiesEndpointsLocalizationResource>(
                "Permission:Activities.Activities.ReadOthers"),
            MultiTenancySides.Both);

        group.AddPermission(
            ActivitiesPermissions.Activities.Manage,
            LocalizableString.Create<ActivitiesEndpointsLocalizationResource>(
                "Permission:Activities.Activities.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            ActivitiesPermissions.Activities.Reassign,
            LocalizableString.Create<ActivitiesEndpointsLocalizationResource>(
                "Permission:Activities.Activities.Reassign"),
            MultiTenancySides.Both);

        group.AddPermission(
            ActivitiesPermissions.Activities.Execute,
            LocalizableString.Create<ActivitiesEndpointsLocalizationResource>(
                "Permission:Activities.Activities.Execute"),
            MultiTenancySides.Both);
    }
}
