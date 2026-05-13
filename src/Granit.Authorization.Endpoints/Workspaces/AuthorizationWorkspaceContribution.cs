using Granit.Authorization.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Authorization.Endpoints.Workspaces;

/// <summary>
/// Grafts authorization admin entries onto the
/// <c>Granit.Framework.Users</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class AuthorizationWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Users)
            .Section("authorization", s => s
                .DisplayKey("AuthorizationEndpoints:Workspace.Section")
                .Order(0)
                .Link("/authorization/permissions", i => i
                    .DisplayKey("AuthorizationEndpoints:Workspace.Definitions")
                    .Icon("key")
                    .Order(0)
                    .RequiresPermission(AuthorizationEndpointsPermissions.Definitions.Read)));
}
