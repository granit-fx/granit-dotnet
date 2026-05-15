using Granit.Privacy.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Privacy.Endpoints.Workspaces;

/// <summary>
/// Grafts privacy admin entries onto the
/// <c>Granit.Framework.Compliance</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class PrivacyWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Compliance)
            .Section("privacy", s => s
                .DisplayKey("PrivacyEndpoints:Workspace.Section")
                .Order(0)
                .Link("/privacy/purposes", i => i
                    .DisplayKey("PrivacyEndpoints:Workspace.Purposes")
                    .Icon("shield-check")
                    .Order(0)
                    .RequiresPermission(PrivacyPermissions.Purposes.Read))
                .Link("/privacy/agreements", i => i
                    .DisplayKey("PrivacyEndpoints:Workspace.Agreements")
                    .Icon("file-signature")
                    .Order(1)
                    .RequiresPermission(PrivacyPermissions.Agreements.Read)));
}
