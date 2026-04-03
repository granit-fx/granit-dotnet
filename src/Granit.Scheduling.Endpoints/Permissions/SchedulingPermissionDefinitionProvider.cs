using Granit.Authorization;
using Granit.Localization;
using Granit.Scheduling.Endpoints.Internal;

namespace Granit.Scheduling.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Scheduling.Actions.Read</c> and <c>Scheduling.Actions.Manage</c>
/// permissions in the Granit RBAC system.
/// </summary>
internal sealed class SchedulingPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            SchedulingPermissions.GroupName,
            LocalizableString.Create<SchedulingEndpointsLocalizationResource>(
                "PermissionGroup:Scheduling"));

        group.AddPermission(
            SchedulingPermissions.Actions.Read,
            LocalizableString.Create<SchedulingEndpointsLocalizationResource>(
                "Permission:Scheduling.Actions.Read"));

        group.AddPermission(
            SchedulingPermissions.Actions.Manage,
            LocalizableString.Create<SchedulingEndpointsLocalizationResource>(
                "Permission:Scheduling.Actions.Manage"));
    }
}
