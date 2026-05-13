using Granit.MultiTenancy.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.MultiTenancy.Endpoints.Workspaces;

/// <summary>
/// Grafts multi-tenancy admin entries onto the
/// <c>Granit.Framework.System</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class MultiTenancyWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.System)
            .Section("multi-tenancy", s => s
                .DisplayKey("MultiTenancyEndpoints:Workspace.Section")
                .Order(30)
                .Link("/multi-tenancy/tenants", i => i
                    .DisplayKey("MultiTenancyEndpoints:Workspace.Tenants")
                    .Icon("building-2")
                    .Order(0)
                    .RequiresPermission(MultiTenancyPermissions.Tenants.Read)));
}
