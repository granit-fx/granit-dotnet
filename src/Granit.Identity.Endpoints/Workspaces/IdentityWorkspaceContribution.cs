using Granit.Identity.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Identity.Endpoints.Workspaces;

/// <summary>
/// Grafts identity admin entries onto the
/// <c>Granit.Framework.IdentityAccess</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class IdentityWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.IdentityAccess)
            .Section("identity", s => s
                .DisplayKey("IdentityEndpoints:Workspace.Section")
                .Order(0)
                .Link("/identity/users", i => i
                    .DisplayKey("IdentityEndpoints:Workspace.Users")
                    .Icon("users-round")
                    .Order(0)
                    .RequiresPermission(IdentityPermissions.Users.Read))
                .Link("/identity/roles", i => i
                    .DisplayKey("IdentityEndpoints:Workspace.Roles")
                    .Icon("shield-user")
                    .Order(1)
                    .RequiresPermission(IdentityPermissions.Roles.Read))
                .Link("/identity/groups", i => i
                    .DisplayKey("IdentityEndpoints:Workspace.Groups")
                    .Icon("users")
                    .Order(2)
                    .RequiresPermission(IdentityPermissions.Groups.Read))
                .Link("/identity/sessions", i => i
                    .DisplayKey("IdentityEndpoints:Workspace.Sessions")
                    .Icon("monitor-smartphone")
                    .Order(3)
                    .RequiresPermission(IdentityPermissions.Sessions.Read)));
}
