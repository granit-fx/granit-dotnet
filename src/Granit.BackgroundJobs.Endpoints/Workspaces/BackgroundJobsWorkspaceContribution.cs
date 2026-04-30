using Granit.BackgroundJobs.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.BackgroundJobs.Endpoints.Workspaces;

/// <summary>
/// Grafts background-jobs admin entries onto the
/// <c>Granit.Framework.Automation</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class BackgroundJobsWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Automation)
            .Section("background-jobs", s => s
                .DisplayKey("BackgroundJobsEndpoints:Workspace.Section")
                .Order(0)
                .Link("/admin/background-jobs", i => i
                    .DisplayKey("BackgroundJobsEndpoints:Workspace.Item")
                    .Icon("clock")
                    .Order(0)
                    .RequiresPermission(BackgroundJobsPermissions.Jobs.Read)));
}
