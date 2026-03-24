using Granit.Authorization.Abstractions;
using Granit.Identity.Endpoints.Internal;
using Granit.Localization;

namespace Granit.Identity.Endpoints.Permissions;

/// <summary>
/// Declares identity user cache permissions in the Granit RBAC system.
/// </summary>
internal sealed class IdentityPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            IdentityUserCachePermissions.GroupName,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "PermissionGroup:Identity"));

        group.AddPermission(
            IdentityUserCachePermissions.UserCache.Read,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.UserCache.Read"));

        group.AddPermission(
            IdentityUserCachePermissions.UserCache.Sync,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.UserCache.Sync"));

        group.AddPermission(
            IdentityUserCachePermissions.UserCache.Delete,
            LocalizableString.Create<IdentityEndpointsLocalizationResource>(
                "Permission:Identity.UserCache.Delete"));
    }
}
