using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Workspaces.Framework.Permissions;

/// <summary>
/// Declares the permission required to enter the Granit framework workspace
/// (<c>Workspace.Granit.Framework.Read</c>). Auto-discovered by
/// <c>GranitAuthorizationModule</c> so any host that loads
/// <see cref="GranitWorkspacesFrameworkModule"/> gets the permission registered
/// without redeclaring it in its own provider.
/// </summary>
internal sealed class FrameworkWorkspacePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            "Workspace.Granit",
            LocalizableString.Create<WorkspacesFrameworkLocalizationResource>(
                "PermissionGroup:Workspace.Granit"));

        group.AddPermission(
            FrameworkWorkspaceNames.FrameworkReadPermission,
            LocalizableString.Create<WorkspacesFrameworkLocalizationResource>(
                "Permission:Workspace.Granit.Framework.Read"),
            MultiTenancySides.Both);
    }
}
