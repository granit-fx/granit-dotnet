using Granit.Authorization;
using Granit.Localization;
using Granit.Timeline.Endpoints.Internal;

namespace Granit.Timeline.Endpoints.Permissions;

/// <summary>
/// Declares all Timeline permissions in the Granit RBAC system.
/// </summary>
internal sealed class TimelinePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            TimelinePermissions.GroupName,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "PermissionGroup:Timeline"));

        group.AddPermission(
            TimelinePermissions.Entries.Read,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "Permission:Timeline.Entries.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            TimelinePermissions.Entries.Create,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "Permission:Timeline.Entries.Create"),
            MultiTenancySide.Both);

        group.AddPermission(
            TimelinePermissions.Entries.Manage,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "Permission:Timeline.Entries.Manage"),
            MultiTenancySide.Both);

        group.AddPermission(
            TimelinePermissions.InternalNotes.Read,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "Permission:Timeline.InternalNotes.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            TimelinePermissions.Followers.Manage,
            LocalizableString.Create<TimelineEndpointsLocalizationResource>(
                "Permission:Timeline.Followers.Manage"),
            MultiTenancySide.Both);
    }
}
