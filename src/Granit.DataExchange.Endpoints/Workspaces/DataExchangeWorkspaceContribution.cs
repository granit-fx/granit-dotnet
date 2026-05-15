using Granit.DataExchange.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.DataExchange.Endpoints.Workspaces;

/// <summary>
/// Grafts data-exchange admin entries onto the
/// <c>Granit.Framework.Storage</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class DataExchangeWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Storage)
            .Section("data-exchange", s => s
                .DisplayKey("DataExchangeEndpoints:Workspace.Section")
                .Order(10)
                .Link("/data-exchange/imports", i => i
                    .DisplayKey("DataExchangeEndpoints:Workspace.Imports")
                    .Icon("download")
                    .Order(0)
                    .RequiresPermission(DataExchangePermissions.Imports.Read))
                .Link("/data-exchange/exports", i => i
                    .DisplayKey("DataExchangeEndpoints:Workspace.Exports")
                    .Icon("upload")
                    .Order(1)
                    .RequiresPermission(DataExchangePermissions.Exports.Read)));
}
