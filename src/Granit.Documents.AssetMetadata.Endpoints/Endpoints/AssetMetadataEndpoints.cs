using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.Endpoints.Dtos;
using Granit.Documents.AssetMetadata.Endpoints.Mapping;
using Granit.Documents.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.AssetMetadata.Endpoints.Endpoints;

/// <summary>
/// HTTP endpoints for reading extracted asset metadata (F17.3). The
/// <c>/metadata</c> endpoint returns the current-version row; the
/// <c>/versions/{versionId}/metadata</c> endpoint addresses a specific version.
/// Both return 404 when the document is missing, excluded by the tenant filter,
/// or has no metadata row yet (extraction is asynchronous — clients retry once
/// the F17.4 background job has completed).
/// </summary>
internal static class AssetMetadataEndpoints
{
    private const string TagName = "Documents - Asset Metadata";

    public static RouteGroupBuilder MapAssetMetadataEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder documents = group.MapGranitGroup("/documents/{id:guid}").WithTags(TagName);

        documents.MapGet("/metadata", GetForCurrentVersionAsync)
            .WithName("GetDocumentAssetMetadata")
            .WithSummary("Returns extracted metadata for the document's current version.")
            .WithDescription(
                "Surfaces the typed projection (camera, dimensions, page count, audio track …) "
                + "plus the verbatim extractor payload under `rawMetadata`. Returns 404 when "
                + "the document is missing, excluded by the tenant filter, or extraction has "
                + "not yet completed for the current version.")
            .RequireAuthorization(DocumentsPermissions.Documents.Read)
            .Produces<AssetMetadataResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        documents.MapGet("/versions/{versionId:guid}/metadata", GetForVersionAsync)
            .WithName("GetDocumentVersionAssetMetadata")
            .WithSummary("Returns extracted metadata for a specific version of the document.")
            .WithDescription(
                "Same shape as the current-version endpoint, addressed by an explicit version "
                + "identifier. Useful for audit trails or comparing metadata between revisions. "
                + "Returns 404 when the document or version is missing, the version does not "
                + "belong to the supplied document, or no metadata row has been produced yet.")
            .RequireAuthorization(DocumentsPermissions.Documents.Read)
            .Produces<AssetMetadataResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return documents;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<AssetMetadataResponse>, NotFound>> GetForCurrentVersionAsync(
        Guid id,
        [FromServices] IAssetMetadataService service,
        CancellationToken cancellationToken)
    {
        DocumentAssetMetadata? row = await service
            .GetForCurrentVersionAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return row is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(row.ToResponse());
    }

    private static async Task<Results<Ok<AssetMetadataResponse>, NotFound>> GetForVersionAsync(
        Guid id,
        Guid versionId,
        [FromServices] IAssetMetadataService service,
        CancellationToken cancellationToken)
    {
        DocumentAssetMetadata? row = await service
            .GetForVersionAsync(id, versionId, cancellationToken)
            .ConfigureAwait(false);
        return row is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(row.ToResponse());
    }
}
