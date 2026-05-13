using Granit.Auditing.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Auditing.Endpoints.Workspaces;

/// <summary>
/// Grafts auditing admin entries onto the
/// <c>Granit.Framework.Monitoring</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class AuditingWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Monitoring)
            .Section("auditing", s => s
                .DisplayKey("AuditingEndpoints:Workspace.Section")
                .Order(0)
                .Link("/audit-log", i => i
                    .DisplayKey("AuditingEndpoints:Workspace.Item")
                    .Icon("clipboard-list")
                    .Order(0)
                    .RequiresPermission(AuditingPermissions.AuditEntries.Read)));
}
