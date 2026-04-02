using Granit.Authorization;
using Granit.Identity.Endpoints.Internal;
using Granit.Localization;

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
                "Permission:Identity.Users.Read"));

        group.AddPermission(
            IdentityPermissions.Users.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Manage"));

        group.AddPermission(
            IdentityPermissions.Users.Sync,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Sync"));

        group.AddPermission(
            IdentityPermissions.Users.Delete,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Delete"));

        // Roles
        group.AddPermission(
            IdentityPermissions.Roles.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Roles.Read"));

        group.AddPermission(
            IdentityPermissions.Roles.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Roles.Manage"));

        // Groups
        group.AddPermission(
            IdentityPermissions.Groups.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Groups.Read"));

        group.AddPermission(
            IdentityPermissions.Groups.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Groups.Manage"));

        // Sessions
        group.AddPermission(
            IdentityPermissions.Sessions.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Sessions.Read"));

        group.AddPermission(
            IdentityPermissions.Sessions.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Sessions.Manage"));

        // Passwords
        group.AddPermission(
            IdentityPermissions.Passwords.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Passwords.Manage"));
    }
}
