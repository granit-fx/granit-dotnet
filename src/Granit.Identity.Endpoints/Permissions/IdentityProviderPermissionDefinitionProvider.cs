using Granit.Authorization.Abstractions;
using Granit.Core.Localization;
using Granit.Identity.Endpoints.Internal;

namespace Granit.Identity.Endpoints.Permissions;

/// <summary>
/// Declares identity provider administration permissions in the Granit RBAC system.
/// </summary>
internal sealed class IdentityProviderPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            IdentityProviderPermissions.GroupName,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "PermissionGroup:Identity"));

        group.AddPermission(
            IdentityProviderPermissions.Users.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Read"));

        group.AddPermission(
            IdentityProviderPermissions.Users.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Users.Manage"));

        group.AddPermission(
            IdentityProviderPermissions.Roles.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Roles.Read"));

        group.AddPermission(
            IdentityProviderPermissions.Roles.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Roles.Manage"));

        group.AddPermission(
            IdentityProviderPermissions.Groups.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Groups.Read"));

        group.AddPermission(
            IdentityProviderPermissions.Groups.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Groups.Manage"));

        group.AddPermission(
            IdentityProviderPermissions.Sessions.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Sessions.Read"));

        group.AddPermission(
            IdentityProviderPermissions.Sessions.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Sessions.Manage"));

        group.AddPermission(
            IdentityProviderPermissions.Passwords.Manage,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.Passwords.Manage"));
    }
}
