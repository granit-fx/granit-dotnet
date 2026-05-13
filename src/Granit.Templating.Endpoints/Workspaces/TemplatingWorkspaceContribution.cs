using Granit.Templating.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Templating.Endpoints.Workspaces;

/// <summary>
/// Grafts templating admin entries onto the
/// <c>Granit.Framework.System</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class TemplatingWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.System)
            .Section("templating", s => s
                .DisplayKey("TemplatingEndpoints:Workspace.Section")
                .Order(40)
                .Link("/templating/templates", i => i
                    .DisplayKey("TemplatingEndpoints:Workspace.Templates")
                    .Icon("layout-template")
                    .Order(0)
                    .RequiresPermission(TemplatingPermissions.Templates.Read))
                .Link("/templating/categories", i => i
                    .DisplayKey("TemplatingEndpoints:Workspace.Categories")
                    .Icon("folder-tree")
                    .Order(1)
                    .RequiresPermission(TemplatingPermissions.Categories.Read)));
}
