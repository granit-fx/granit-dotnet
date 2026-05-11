using System;
using Granit.Documents.AssetMetadata.Endpoints.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.AssetMetadata.Endpoints.Extensions;

/// <summary>Maps the asset-metadata HTTP surface onto a host route group.</summary>
public static class AssetMetadataEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the asset-metadata endpoints under the supplied <paramref name="group"/>
    /// (typically the same group hosting <c>MapGranitDocuments</c>). Permission gating
    /// inherits <c>Documents.Documents.Read</c>; no metadata-specific permissions
    /// are introduced.
    /// </summary>
    public static RouteGroupBuilder MapGranitDocumentsAssetMetadata(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapAssetMetadataEndpoints();
        return group;
    }
}
