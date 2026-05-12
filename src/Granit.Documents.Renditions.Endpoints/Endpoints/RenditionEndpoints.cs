using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents.Permissions;
using Granit.Documents.Renditions;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Endpoints.Dtos;
using Granit.Documents.Renditions.Endpoints.Mapping;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Renditions.Endpoints.Endpoints;

/// <summary>
/// HTTP endpoints for listing and downloading document renditions (F16.3). The list
/// endpoint surfaces every rendition row for the document's current version (any status,
/// so callers can render placeholders for <c>Pending</c> / <c>Generating</c>); the
/// download endpoint returns a presigned URL for a <c>Ready</c> rendition.
/// </summary>
internal static class RenditionEndpoints
{
    private const string TagName = "Documents - Renditions";

    public static RouteGroupBuilder MapRenditionEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder renditions = group.MapGranitGroup("/documents/{id:guid}/renditions").WithTags(TagName);

        renditions.MapGet("", ListAsync)
            .WithName("ListDocumentRenditions")
            .WithSummary("Lists every rendition for a document's current version.")
            .WithDescription(
                "Returns each rendition row (any status — including Pending and Failed) so the "
                + "UI can render placeholders or surface errors. Ordered by Type then Format. "
                + "Returns 404 when the document is not found or has no current version.")
            .RequireAuthorization(DocumentsPermissions.Documents.Read)
            .Produces<ListRenditionsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        renditions.MapGet("/{type}/download", DownloadAsync)
            .WithName("RequestRenditionDownloadUrl")
            .WithSummary("Issues a presigned download URL for a Ready rendition.")
            .WithDescription(
                "Returns a short-lived presigned URL the client can use to download the "
                + "rendition bytes directly from blob storage. Pass `?format=image/webp` to "
                + "pick a specific MIME when several renditions of the requested Type are "
                + "Ready; otherwise the first Ready row wins. Returns 404 when the document "
                + "is missing or no rendition of the requested Type / Format is yet in Ready "
                + "status — on-demand synchronous generation is deferred to a follow-up; "
                + "until then callers retry once the F16.4 background job completes.")
            .RequireAuthorization(DocumentsPermissions.Documents.Read)
            .Produces<RenditionDownloadUrlResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return renditions;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<ListRenditionsResponse>, NotFound>> ListAsync(
        Guid id,
        [FromServices] IRenditionService service,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentRendition>? rows =
            await service.ListAsync(id, cancellationToken).ConfigureAwait(false);
        if (rows is null)
        {
            return TypedResults.NotFound();
        }

        // The service guarantees rows[0..].DocumentVersionId is the document's current
        // version when the list is non-empty. For empty lists we look up the version on
        // the document — but rows is never null here, so fall back to Guid.Empty in the
        // rare empty case (no renditions yet) and surface the document id only.
        Guid versionId = rows.Count > 0 ? rows[0].DocumentVersionId : Guid.Empty;
        return TypedResults.Ok(rows.ToListResponse(id, versionId));
    }

    private static async Task<Results<Ok<RenditionDownloadUrlResponse>, NotFound>> DownloadAsync(
        Guid id,
        RenditionType type,
        [FromQuery] string? format,
        [FromServices] IRenditionService service,
        CancellationToken cancellationToken)
    {
        PresignedDownloadUrl? url = await service
            .GetDownloadUrlAsync(id, type, format, cancellationToken)
            .ConfigureAwait(false);
        return url is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(url.ToResponse());
    }
}
