using Granit.Authorization;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Minimal provider that declares the three <c>IdentityLocal.Roles.*</c> permissions
/// under test. Matches the production <c>IdentityLocalPermissionDefinitionProvider</c>
/// (internal) in name and <see cref="MultiTenancySide"/> without pulling
/// <c>InternalsVisibleTo</c> just for the tests.
/// </summary>
internal sealed class TestPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            IdentityLocalPermissions.GroupName,
            LocalizableString.Fixed($"PermissionGroup:{IdentityLocalPermissions.GroupName}"));

        group.AddPermission(
            IdentityLocalPermissions.Roles.Read,
            LocalizableString.Fixed("Permission:IdentityLocal.Roles.Read"),
            MultiTenancySide.Both);
        group.AddPermission(
            IdentityLocalPermissions.Roles.Manage,
            LocalizableString.Fixed("Permission:IdentityLocal.Roles.Manage"),
            MultiTenancySide.Both);
        group.AddPermission(
            IdentityLocalPermissions.Roles.Delete,
            LocalizableString.Fixed("Permission:IdentityLocal.Roles.Delete"),
            MultiTenancySide.Both);
    }
}
