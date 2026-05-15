using Granit.Diagnostics.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Diagnostics.Endpoints.Workspaces;

/// <summary>
/// Grafts diagnostics admin entries onto the
/// <c>Granit.Framework.Observability</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class DiagnosticsWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Observability)
            .Section("diagnostics", s => s
                .DisplayKey("DiagnosticsEndpoints:Workspace.Section")
                .Order(20)
                .Link("/diagnostics", i => i
                    .DisplayKey("DiagnosticsEndpoints:Workspace.Item")
                    .Icon("heart-pulse")
                    .Order(0)
                    .RequiresPermission(DiagnosticsPermissions.Monitoring.Read)));
}
