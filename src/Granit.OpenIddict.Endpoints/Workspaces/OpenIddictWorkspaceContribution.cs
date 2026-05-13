using Granit.OpenIddict.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.OpenIddict.Endpoints.Workspaces;

/// <summary>
/// Grafts OpenID Connect (OIDC) admin entries onto the
/// <c>Granit.Framework.Users</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class OpenIddictWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Users)
            .Section("openiddict", s => s
                .DisplayKey("OpenIddictEndpoints:Workspace.Section")
                .Order(30)
                .Link("/oidc/applications", i => i
                    .DisplayKey("OpenIddictEndpoints:Workspace.Applications")
                    .Icon("app-window")
                    .Order(0)
                    .RequiresPermission(OpenIddictPermissions.Applications.Read))
                .Link("/oidc/scopes", i => i
                    .DisplayKey("OpenIddictEndpoints:Workspace.Scopes")
                    .Icon("scan")
                    .Order(1)
                    .RequiresPermission(OpenIddictPermissions.Scopes.Read))
                .Link("/oidc/authorizations", i => i
                    .DisplayKey("OpenIddictEndpoints:Workspace.Authorizations")
                    .Icon("badge-check")
                    .Order(2)
                    .RequiresPermission(OpenIddictPermissions.Authorizations.Read)));
}
