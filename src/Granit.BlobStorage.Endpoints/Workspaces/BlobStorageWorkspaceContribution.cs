using Granit.BlobStorage.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.BlobStorage.Endpoints.Workspaces;

/// <summary>
/// Grafts blob-storage admin entries onto the
/// <c>Granit.Framework.Storage</c> shell (per ADR-040 §IoC). Permission gate
/// drops the link from the manifest payload when the caller cannot read
/// blob storage.
/// </summary>
internal sealed class BlobStorageWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Storage)
            .Section("blob-storage", s => s
                .DisplayKey("BlobStorageEndpoints:Workspace.Section")
                .Order(0)
                .Link("/blob-storage", i => i
                    .DisplayKey("BlobStorageEndpoints:Workspace.Item")
                    .Icon("file")
                    .Order(0)
                    .RequiresPermission(BlobStoragePermissions.Administration.Read)));
}
