using Granit.Authorization;
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

        // OIDC Applications
        group.AddPermission(
            OpenIddictPermissions.Applications.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Read"));
        group.AddPermission(
            OpenIddictPermissions.Applications.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Manage"));
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
            OpenIddictPermissions.Scopes.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Scopes.Manage"));

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
