using Granit.BlobStorage.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.BlobStorage.Endpoints.Workspaces;

/// <summary>Declares the blob-storage module's features (per ADR-057).</summary>
internal sealed class BlobStorageFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(BlobStorageFeatures.Administration, f => f
            .Permission(BlobStoragePermissions.Administration.Read)
            .RouteName(BlobStorageFeatures.Administration)
            .DefaultIcon("file")
            .DisplayKey("BlobStorageEndpoints:Workspace.Item"));
}
