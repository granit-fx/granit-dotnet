using Granit.Authentication.ApiKeys.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Authentication.ApiKeys.Endpoints.Workspaces;

/// <summary>
/// Grafts API-key admin entries onto the
/// <c>Granit.Framework.Users</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class ApiKeysWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Users)
            .Section("api-keys", s => s
                .DisplayKey("AuthenticationApiKeysEndpoints:Workspace.Section")
                .Order(10)
                .Link("/api-keys", i => i
                    .DisplayKey("AuthenticationApiKeysEndpoints:Workspace.Item")
                    .Icon("key-round")
                    .Order(0)
                    .RequiresPermission(ApiKeyPermissions.Keys.Read)));
}
