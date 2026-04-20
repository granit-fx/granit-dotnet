using Granit.Authorization;
using Granit.Identity.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Identity.Endpoints.Permissions;

/// <summary>
/// Declares all identity permissions in the Granit RBAC system.
/// </summary>
internal sealed class IdentityPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            IdentityPermissions.GroupName,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "PermissionGroup:Identity"));

        // Users
        group.AddPermission(
            IdentityPermissions.Users.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityPermissions.Users.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Manage"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityPermissions.Users.Sync,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Sync"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityPermissions.Users.Delete,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Delete"),
            MultiTenancySide.Both);

        // Roles
        group.AddPermission(
            IdentityPermissions.Roles.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Roles.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityPermissions.Roles.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Roles.Manage"),
            MultiTenancySide.Both);

        // Groups
        group.AddPermission(
            IdentityPermissions.Groups.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Groups.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityPermissions.Groups.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Groups.Manage"),
            MultiTenancySide.Both);

        // Sessions
        group.AddPermission(
            IdentityPermissions.Sessions.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Sessions.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityPermissions.Sessions.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Sessions.Manage"),
            MultiTenancySide.Both);

        // Passwords
        group.AddPermission(
            IdentityPermissions.Passwords.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Passwords.Manage"),
            MultiTenancySide.Both);
    }
}
