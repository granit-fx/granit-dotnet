using Granit.Authorization.Abstractions;
using Granit.Localization;
using Granit.OpenIddict.Endpoints.Internal;
using Granit.OpenIddict.Permissions;

namespace Granit.OpenIddict.Endpoints.Permissions;

/// <summary>
/// Registers all OpenIddict permissions with the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class OpenIddictPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            OpenIddictPermissions.GroupName,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "PermissionGroup:OpenIddict"));

        // Users
        group.AddPermission(
            OpenIddictPermissions.Users.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Users.Read"));
        group.AddPermission(
            OpenIddictPermissions.Users.Create,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Users.Create"));
        group.AddPermission(
            OpenIddictPermissions.Users.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Users.Manage"));
        group.AddPermission(
            OpenIddictPermissions.Users.Delete,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Users.Delete"));
        group.AddPermission(
            OpenIddictPermissions.Users.Impersonate,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Users.Impersonate"));

        // Roles
        group.AddPermission(
            OpenIddictPermissions.Roles.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Roles.Read"));
        group.AddPermission(
            OpenIddictPermissions.Roles.Create,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Roles.Create"));
        group.AddPermission(
            OpenIddictPermissions.Roles.Delete,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Roles.Delete"));

        // Groups
        group.AddPermission(
            OpenIddictPermissions.Groups.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Groups.Read"));
        group.AddPermission(
            OpenIddictPermissions.Groups.Create,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Groups.Create"));
        group.AddPermission(
            OpenIddictPermissions.Groups.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Groups.Manage"));
        group.AddPermission(
            OpenIddictPermissions.Groups.Delete,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Groups.Delete"));

        // OIDC Applications
        group.AddPermission(
            OpenIddictPermissions.Applications.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Read"));
        group.AddPermission(
            OpenIddictPermissions.Applications.Create,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Create"));
        group.AddPermission(
            OpenIddictPermissions.Applications.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Manage"));
        group.AddPermission(
            OpenIddictPermissions.Applications.Delete,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Delete"));
        group.AddPermission(
            OpenIddictPermissions.Applications.Rotate,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Rotate"));

        // OIDC Scopes
        group.AddPermission(
            OpenIddictPermissions.Scopes.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Scopes.Read"));
        group.AddPermission(
            OpenIddictPermissions.Scopes.Create,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Scopes.Create"));
        group.AddPermission(
            OpenIddictPermissions.Scopes.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Scopes.Manage"));
        group.AddPermission(
            OpenIddictPermissions.Scopes.Delete,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Scopes.Delete"));

        // OIDC Authorizations
        group.AddPermission(
            OpenIddictPermissions.Authorizations.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Authorizations.Read"));
        group.AddPermission(
            OpenIddictPermissions.Authorizations.Revoke,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Authorizations.Revoke"));
    }
}
