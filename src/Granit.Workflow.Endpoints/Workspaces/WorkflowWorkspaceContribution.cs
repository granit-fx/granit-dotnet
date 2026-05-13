using Granit.Workflow.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Workflow.Endpoints.Workspaces;

/// <summary>
/// Grafts workflow admin entries onto the
/// <c>Granit.Framework.Automation</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class WorkflowWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Automation)
            .Section("workflow", s => s
                .DisplayKey("WorkflowEndpoints:Workspace.Section")
                .Order(10)
                .Link("/workflow/history", i => i
                    .DisplayKey("WorkflowEndpoints:Workspace.History")
                    .Icon("git-branch")
                    .Order(0)
                    .RequiresPermission(WorkflowPermissions.History.Read)));
}
