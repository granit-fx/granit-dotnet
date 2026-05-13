using Granit.Scheduling.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Scheduling.Endpoints.Workspaces;

/// <summary>
/// Grafts scheduling admin entries onto the
/// <c>Granit.Framework.Automation</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class SchedulingWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Automation)
            .Section("scheduling", s => s
                .DisplayKey("SchedulingEndpoints:Workspace.Section")
                .Order(20)
                .Link("/scheduling", i => i
                    .DisplayKey("SchedulingEndpoints:Workspace.Item")
                    .Icon("calendar-clock")
                    .Order(0)
                    .RequiresPermission(SchedulingPermissions.Actions.Read)));
}
