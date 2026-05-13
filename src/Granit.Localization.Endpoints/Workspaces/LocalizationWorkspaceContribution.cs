using Granit.Localization.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Localization.Endpoints.Workspaces;

/// <summary>
/// Grafts localization-overrides admin entries onto the
/// <c>Granit.Framework.System</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class LocalizationWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.System)
            .Section("localization", s => s
                .DisplayKey("LocalizationEndpoints:Workspace.Section")
                .Order(20)
                .Link("/localization/overrides", i => i
                    .DisplayKey("LocalizationEndpoints:Workspace.Item")
                    .Icon("languages")
                    .Order(0)
                    .RequiresPermission(LocalizationOverridesPermissions.Overrides.Read)));
}
