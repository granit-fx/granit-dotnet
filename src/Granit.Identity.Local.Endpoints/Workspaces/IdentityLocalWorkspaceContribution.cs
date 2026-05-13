using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Identity.Local.Endpoints.Workspaces;

/// <summary>
/// Grafts local-identity admin entries onto the
/// <c>Granit.Framework.Users</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class IdentityLocalWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Users)
            .Section("identity-local", s => s
                .DisplayKey("IdentityLocalEndpoints:Workspace.Section")
                .Order(20)
                .Link("/identity-local/roles", i => i
                    .DisplayKey("IdentityLocalEndpoints:Workspace.Roles")
                    .Icon("shield-check")
                    .Order(0)
                    .RequiresPermission(IdentityLocalPermissions.Roles.Read)));
}
