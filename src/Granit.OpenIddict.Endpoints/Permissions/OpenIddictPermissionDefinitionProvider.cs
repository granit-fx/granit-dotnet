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

        // Users (impersonation only — user CRUD is handled by Granit.Identity.Endpoints)
        group.AddPermission(
            OpenIddictPermissions.Users.Impersonate,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Users.Impersonate"));

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
