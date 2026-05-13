using Granit.Settings.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Settings.Endpoints.Workspaces;

/// <summary>
/// Grafts settings admin entries onto the
/// <c>Granit.Framework.System</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class SettingsWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.System)
            .Section("settings", s => s
                .DisplayKey("SettingsEndpoints:Workspace.Section")
                .Order(0)
                .Link("/settings/global", i => i
                    .DisplayKey("SettingsEndpoints:Workspace.Global")
                    .Icon("settings-2")
                    .Order(0)
                    .RequiresPermission(SettingsPermissions.Global.Read))
                .Link("/settings/tenant", i => i
                    .DisplayKey("SettingsEndpoints:Workspace.Tenant")
                    .Icon("building")
                    .Order(1)
                    .RequiresPermission(SettingsPermissions.Tenant.Read)));
}
