using Granit.Authorization;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Localization;

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

        group.AddPermission(
            IdentityLocalPermissions.Users.Impersonate,
            LocalizableString.Create<IdentityLocalEndpointsLocalizationResource>(
                "Permission:IdentityLocal.Users.Impersonate"));
    }
}
