using Granit.Authorization;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Identity.Local.Endpoints.Permissions;

/// <summary>
/// Registers all Identity.Local permissions with the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class IdentityLocalPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            IdentityLocalPermissions.GroupName,
            LocalizableString.Create<IdentityLocalEndpointsLocalizationResource>(
                "PermissionGroup:IdentityLocal"));

        // Impersonation is a highly privileged "break glass" capability intended for platform
        // support / investigations — cross-tenant by design. Restrict to the host side.
        group.AddPermission(
            IdentityLocalPermissions.Users.Impersonate,
            LocalizableString.Create<IdentityLocalEndpointsLocalizationResource>(
                "Permission:IdentityLocal.Users.Impersonate"),
            MultiTenancySide.Host);

        // Role CRUD — assignable in both host and tenant admin contexts. Visibility matrix
        // + the AllowTenantRoles feature flag on endpoint options gate who can create
        // tenant-scoped roles; the permission itself stays Side=Both so either admin can
        // hold it.
        group.AddPermission(
            IdentityLocalPermissions.Roles.Read,
            LocalizableString.Create<IdentityLocalEndpointsLocalizationResource>(
                "Permission:IdentityLocal.Roles.Read"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityLocalPermissions.Roles.Manage,
            LocalizableString.Create<IdentityLocalEndpointsLocalizationResource>(
                "Permission:IdentityLocal.Roles.Manage"),
            MultiTenancySide.Both);

        group.AddPermission(
            IdentityLocalPermissions.Roles.Delete,
            LocalizableString.Create<IdentityLocalEndpointsLocalizationResource>(
                "Permission:IdentityLocal.Roles.Delete"),
            MultiTenancySide.Both);
    }
}
