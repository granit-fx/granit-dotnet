using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy.Authorization.Internal;

namespace Granit.MultiTenancy.Authorization.Permissions;

/// <summary>
/// Declares the <c>MultiTenancy.Host.Impersonate</c> permission.
/// </summary>
internal sealed class MultiTenancyAuthorizationPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            MultiTenancyAuthorizationPermissions.GroupName,
            LocalizableString.Create<MultiTenancyAuthorizationLocalizationResource>(
                "PermissionGroup:MultiTenancy"));

        // Host-side only: a Tenant user can never grant himself the right to impersonate
        // a different tenant by virtue of being inside a tenant scope.
        group.AddPermission(
            MultiTenancyAuthorizationPermissions.Host.Impersonate,
            LocalizableString.Create<MultiTenancyAuthorizationLocalizationResource>(
                "Permission:MultiTenancy.Host.Impersonate"),
            MultiTenancySides.Host);
    }
}
