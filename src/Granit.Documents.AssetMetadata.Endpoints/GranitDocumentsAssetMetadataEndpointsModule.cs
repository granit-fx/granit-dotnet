using Granit.Documents.Endpoints;
using Granit.Modularity;

namespace Granit.Documents.AssetMetadata.Endpoints;

/// <summary>
/// Granit module for the asset-metadata HTTP surface. Composes on top of the
/// metadata base (<see cref="GranitDocumentsAssetMetadataModule"/>) and the
/// parent Documents endpoints so the permission registry is shared — metadata
/// inherits <c>Documents.Documents.Read</c> without declaring its own.
/// </summary>
[DependsOn(
    typeof(GranitDocumentsAssetMetadataModule),
    typeof(GranitDocumentsEndpointsModule))]
public sealed class GranitDocumentsAssetMetadataEndpointsModule : GranitModule;
