using Granit.AI.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.AI.Endpoints.Workspaces;

/// <summary>
/// Grafts AI admin entries onto the
/// <c>Granit.Framework.AI</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class AIWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.AI)
            .Section("ai", s => s
                .DisplayKey("AIEndpoints:Workspace.Section")
                .Order(0)
                .Link("/ai/workspaces", i => i
                    .DisplayKey("AIEndpoints:Workspace.Workspaces")
                    .Icon("sparkles")
                    .Order(0)
                    .RequiresPermission(AIPermissions.Workspaces.Read))
                .Link("/ai/usage", i => i
                    .DisplayKey("AIEndpoints:Workspace.Usage")
                    .Icon("chart-bar")
                    .Order(1)
                    .RequiresPermission(AIPermissions.Usage.Read)));
}
