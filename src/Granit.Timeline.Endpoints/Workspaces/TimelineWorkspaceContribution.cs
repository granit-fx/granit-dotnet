using Granit.Timeline.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Timeline.Endpoints.Workspaces;

/// <summary>
/// Grafts timeline admin entries onto the
/// <c>Granit.Framework.Monitoring</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class TimelineWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Monitoring)
            .Section("timeline", s => s
                .DisplayKey("TimelineEndpoints:Workspace.Section")
                .Order(10)
                .Link("/timeline", i => i
                    .DisplayKey("TimelineEndpoints:Workspace.Item")
                    .Icon("activity")
                    .Order(0)
                    .RequiresPermission(TimelinePermissions.Entries.Read)));
}
