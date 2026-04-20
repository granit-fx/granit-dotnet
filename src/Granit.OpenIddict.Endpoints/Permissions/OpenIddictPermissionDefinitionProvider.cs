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

        // OIDC Applications — GranitOpenIddictApplication is IMultiTenant: apps can be
        // tenant-scoped or host-level, so management permissions are valid in either context.
        group.AddPermission(
            OpenIddictPermissions.Applications.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Read"),
            MultiTenancySide.Both);
        group.AddPermission(
            OpenIddictPermissions.Applications.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Manage"),
            MultiTenancySide.Both);
        group.AddPermission(
            OpenIddictPermissions.Applications.Rotate,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Applications.Rotate"),
            MultiTenancySide.Both);

        // OIDC Scopes — scopes are a global OAuth primitive by spec (RFC 6749 §3.3):
        // they identify capabilities, not resource ownership. Managing them is host-level.
        group.AddPermission(
            OpenIddictPermissions.Scopes.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Scopes.Read"),
            MultiTenancySide.Host);
        group.AddPermission(
            OpenIddictPermissions.Scopes.Manage,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Scopes.Manage"),
            MultiTenancySide.Host);

        // OIDC Authorizations — live authorizations can belong to a tenant user or a host
        // admin; the permission must be usable in both contexts.
        group.AddPermission(
            OpenIddictPermissions.Authorizations.Read,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Authorizations.Read"),
            MultiTenancySide.Both);
        group.AddPermission(
            OpenIddictPermissions.Authorizations.Revoke,
            LocalizableString.Create<OpenIddictEndpointsLocalizationResource>(
                "Permission:OpenIddict.Authorizations.Revoke"),
            MultiTenancySide.Both);
    }
}
