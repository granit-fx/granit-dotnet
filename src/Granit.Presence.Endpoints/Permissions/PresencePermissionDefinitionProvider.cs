using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Presence.Endpoints.Internal;

namespace Granit.Presence.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Presence.*</c> permissions in the Granit RBAC system.
/// </summary>
internal sealed class PresencePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PermissionGroup group = context.AddGroup(
            PresencePermissions.GroupName,
            LocalizableString.Create<PresenceEndpointsLocalizationResource>(
                "PermissionGroup:Presence"));

        group.AddPermission(
            PresencePermissions.Self.Manage,
            LocalizableString.Create<PresenceEndpointsLocalizationResource>(
                "Permission:Presence.Self.Manage"),
            MultiTenancySides.Both);

        group.AddPermission(
            PresencePermissions.Users.Read,
            LocalizableString.Create<PresenceEndpointsLocalizationResource>(
                "Permission:Presence.Users.Read"),
            MultiTenancySides.Both);
    }
}
