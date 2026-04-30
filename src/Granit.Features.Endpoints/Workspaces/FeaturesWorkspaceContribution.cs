using Granit.Features.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Features.Endpoints.Workspaces;

/// <summary>
/// Grafts feature-flags admin entries onto the
/// <c>Granit.Framework.System</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class FeaturesWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.System)
            .Section("features", s => s
                .DisplayKey("FeaturesEndpoints:Workspace.Section")
                .Order(10)
                .Link("/admin/features", i => i
                    .DisplayKey("FeaturesEndpoints:Workspace.Item")
                    .Icon("flag")
                    .Order(0)
                    .RequiresPermission(FeaturesPermissions.Flags.Read)));
}
