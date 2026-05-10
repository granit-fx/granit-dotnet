using Granit.BlobStorage;
using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;
using Granit.Documents.Endpoints.Documents.Mapping;
using Granit.Documents.Endpoints.Permissions;
using Granit.Documents.Exceptions;
using Granit.Documents.Options;
using Granit.Timing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Endpoints.Documents.Endpoints;

/// <summary>
/// HTTP endpoints for the document upload flow (F3.2): presigned upload-ticket request
/// and the post-upload finalise step that creates the Document + initial DocumentVersion.
/// </summary>
internal static class DocumentEndpoints
{
    private const string TagName = "Documents";

    public static RouteGroupBuilder MapDocumentEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder documents = group.MapGroup("/documents").WithTags(TagName);

        documents.MapPost("/upload-ticket", RequestUploadTicketAsync)
            .WithName("RequestDocumentUploadTicket")
            .WithSummary("Issues a presigned upload ticket for direct client-to-cloud transfer.")
            .WithDescription(
                "Issues a presigned PUT URL backed by Granit.BlobStorage. The client uploads the "
                + "bytes directly to the configured cloud provider (no proxying through the API), "
                + "then calls POST /documents/finalize with the returned blobId. Tickets expire "
                + "after the BlobStorage-configured TTL (default 15 minutes).")
            .RequireAuthorization(p => p.RequireClaim(
                "permission", DocumentsPermissions.Documents.Manage))
            .Produces<UploadTicketResponse>()
            .ProducesValidationProblem();

        documents.MapPost("/finalize", FinalizeUploadAsync)
            .WithName("FinalizeDocumentUpload")
            .WithSummary("Confirms the upload and creates the Document + initial version.")
            .WithDescription(
                "Confirms with BlobStorage that the bytes have arrived and pass validation, "
                + "then atomically creates the Document aggregate plus its initial v1 "
                + "DocumentVersion under the requested folder (or the tenant root when "
                + "folderId is omitted). Returns 422 when the blob fails validation, 404 when "
                + "the target folder is missing.")
            .RequireAuthorization(p => p.RequireClaim(
                "permission", DocumentsPermissions.Documents.Manage))
            .Produces<DocumentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        documents.MapPost("/{id:guid}/versions", AppendVersionAsync)
            .WithName("AppendDocumentVersion")
            .WithSummary("Appends a new version to an existing document.")
            .WithDescription(
                "Confirms with BlobStorage that the bytes have arrived and pass validation, "
                + "then atomically appends a new DocumentVersion under the parent document "
                + "with VersionNumber = max + 1 and updates Document.CurrentVersionId. "
                + "Optimistic concurrency on Document.RowVersion plus the unique index on "
                + "(DocumentId, VersionNumber) protect concurrent uploaders. Returns 422 "
                + "when the blob fails validation or the document is trashed; 404 when the "
                + "document is missing or excluded by the tenant filter.")
            .RequireAuthorization(p => p.RequireClaim(
                "permission", DocumentsPermissions.Documents.Manage))
            .Produces<DocumentVersionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        documents.MapGet("/{id:guid}/versions", ListVersionsAsync)
            .WithName("ListDocumentVersions")
            .WithSummary("Lists the version history of a document.")
            .WithDescription(
                "Returns a paged slice of the document's version history ordered by "
                + "VersionNumber descending (latest first). The currently-active version "
                + "is flagged via `isCurrent: true`. Use `?skip=` and `?take=` to page; "
                + "defaults are skip=0, take=50 (max 200). Returns 404 when the document "
                + "is missing or excluded by the tenant filter.")
            .RequireAuthorization(p => p.RequireClaim(
                "permission", DocumentsPermissions.Documents.Read))
            .Produces<ListDocumentVersionsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        documents.MapGet("/trash", ListTrashedAsync)
            .WithName("ListTrashedDocuments")
            .WithSummary("Lists trashed documents for the current tenant (F8.2).")
            .WithDescription(
                "Returns a paged slice of trashed documents ordered by `TrashedAt` "
                + "descending. Each row carries `daysUntilPermanentDeletion`, derived from "
                + "the trash retention window (`GranitDocumentsOptions.TrashRetentionDays`, "
                + "default 30 days). Defaults are `skip=0` and `take=50` (max 200).")
            .RequireAuthorization(p => p.RequireClaim(
                "permission", DocumentsPermissions.Documents.Read))
            .Produces<ListTrashedDocumentsResponse>();

        documents.MapDocumentMutationEndpoints();

        documents.MapGet("/{id:guid}/download", DownloadAsync)
            .WithName("RequestDocumentDownloadUrl")
            .WithSummary("Issues a presigned download URL for a document version.")
            .WithDescription(
                "Returns a JSON response carrying a short-lived presigned URL the client can "
                + "use to download the bytes directly from the configured cloud provider. "
                + "Defaults to the document's CurrentVersionId; pass `?versionId={guid}` to "
                + "download a specific historical version. Emits a DocumentDownloadedEvent for "
                + "the ISO 27001 audit trail and increments granit.documents.download.count. "
                + "Returns 404 when the document or version is not found, 409 when the document "
                + "is trashed or has no current version yet.")
            .RequireAuthorization(p => p.RequireClaim(
                "permission", DocumentsPermissions.Documents.Read))
            .Produces<DownloadUrlResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return documents;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Ok<UploadTicketResponse>> RequestUploadTicketAsync(
        UploadTicketRequest request,
        [FromServices] IDocumentService documents,
        CancellationToken cancellationToken)
    {
        PresignedUploadTicket ticket = await documents
            .RequestUploadTicketAsync(request.FileName, request.ContentType, request.MaxAllowedBytes, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(ticket.ToResponse());
    }

    private static async Task<Results<Created<DocumentResponse>, ProblemHttpResult>> FinalizeUploadAsync(
        FinalizeUploadRequest request,
        [FromServices] IDocumentService documents,
        [FromServices] IHttpContextAccessor accessor,
        CancellationToken cancellationToken)
    {
        Guid ownerUserId = ResolveOwnerUserId(accessor.HttpContext);

        try
        {
            Document document = await documents
                .FinalizeUploadAsync(
                    request.BlobId,
                    request.FolderId,
                    ownerUserId,
                    request.Name,
                    request.Description,
                    request.CommitMessage,
                    cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Created($"/documents/{document.Id}", document.ToResponse());
        }
        catch (TenantStorageQuotaExceededException ex)
        {
            return QuotaExceededProblem(ex);
        }
        catch (InvalidOperationException ex)
        {
            // Validation failures (blob rejected) and missing-target-folder. Status 422
            // is the right granularity here for both.
            return TypedResults.Problem(
                ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<Created<DocumentVersionResponse>, ProblemHttpResult>> AppendVersionAsync(
        Guid id,
        AppendVersionRequest request,
        [FromServices] IDocumentService documents,
        [FromServices] IHttpContextAccessor accessor,
        CancellationToken cancellationToken)
    {
        Guid uploadedByUserId = ResolveOwnerUserId(accessor.HttpContext);

        try
        {
            DocumentVersion? version = await documents
                .AppendVersionAsync(id, request.BlobId, uploadedByUserId, request.CommitMessage, cancellationToken)
                .ConfigureAwait(false);
            if (version is null)
            {
                return TypedResults.Problem(
                    $"Document '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return TypedResults.Created(
                $"/documents/{id}/versions/{version.Id}",
                version.ToResponse(isCurrent: true));
        }
        catch (TenantStorageQuotaExceededException ex)
        {
            return QuotaExceededProblem(ex);
        }
        catch (InvalidOperationException ex)
        {
            // Blob validation failure or trashed-document append.
            return TypedResults.Problem(
                ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    /// <summary>
    /// Builds the RFC 7807 problem document returned when an upload would push the tenant
    /// past its storage quota (F7.2). The <c>type</c> URI is the stable identifier the
    /// frontend matches on to render a quota-specific error UI.
    /// </summary>
    private static ProblemHttpResult QuotaExceededProblem(TenantStorageQuotaExceededException ex) =>
        TypedResults.Problem(
            detail: ex.Message,
            statusCode: StatusCodes.Status403Forbidden,
            type: "https://granit.dev/problems/quota-exceeded",
            title: "Tenant storage quota exceeded");

    private static async Task<Ok<ListTrashedDocumentsResponse>> ListTrashedAsync(
        [FromServices] IDocumentService documents,
        [FromServices] IClock clock,
        [FromServices] IOptions<GranitDocumentsOptions> options,
        CancellationToken cancellationToken,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null)
    {
        const int DefaultTake = 50;
        const int MaxTake = 200;

        int effectiveSkip = Math.Max(0, skip ?? 0);
        int effectiveTake = Math.Clamp(take ?? DefaultTake, 1, MaxTake);

        TrashedDocumentPage page = await documents
            .ListTrashedAsync(effectiveSkip, effectiveTake, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        int retentionDays = options.Value.TrashRetentionDays;

        TrashedDocumentResponse[] mapped = [.. page.Documents.Select(d =>
        {
            DateTimeOffset deletionAt = d.TrashedAt.AddDays(retentionDays);
            int daysLeft = Math.Max(0, (int)Math.Ceiling((deletionAt - now).TotalDays));
            return new TrashedDocumentResponse(
                d.Id, d.FolderId, d.Name, d.OwnerUserId, d.TrashedAt, daysLeft);
        })];

        return TypedResults.Ok(new ListTrashedDocumentsResponse(
            mapped, page.TotalCount, effectiveSkip, effectiveTake));
    }

    private static async Task<Results<Ok<ListDocumentVersionsResponse>, ProblemHttpResult>> ListVersionsAsync(
        Guid id,
        [FromServices] IDocumentService documents,
        CancellationToken cancellationToken,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null)
    {
        const int DefaultTake = 50;
        const int MaxTake = 200;

        int effectiveSkip = Math.Max(0, skip ?? 0);
        int effectiveTake = Math.Clamp(take ?? DefaultTake, 1, MaxTake);

        DocumentVersionPage? page = await documents
            .ListVersionsAsync(id, effectiveSkip, effectiveTake, cancellationToken)
            .ConfigureAwait(false);
        if (page is null)
        {
            return TypedResults.Problem(
                $"Document '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return TypedResults.Ok(page.ToResponse(effectiveSkip, effectiveTake));
    }

    private static async Task<Results<Ok<DownloadUrlResponse>, ProblemHttpResult>> DownloadAsync(
        Guid id,
        [FromQuery] Guid? versionId,
        [FromServices] IDocumentService documents,
        [FromServices] IHttpContextAccessor accessor,
        CancellationToken cancellationToken)
    {
        Guid requestedByUserId = ResolveOwnerUserId(accessor.HttpContext);

        try
        {
            BlobStorage.PresignedDownloadUrl? url = await documents
                .RequestDownloadUrlAsync(id, versionId, requestedByUserId, cancellationToken)
                .ConfigureAwait(false);
            if (url is null)
            {
                return TypedResults.Problem(
                    versionId is null
                        ? $"Document '{id}' was not found."
                        : $"Document '{id}' or version '{versionId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return TypedResults.Ok(url.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            // Trashed / permanently-deleted document or no current version yet.
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static Guid ResolveOwnerUserId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return Guid.Empty;
        }

        // Mirrors FolderEndpoints.ResolveOwnerUserId — the User aggregate id (ADR-051) is
        // exposed as ClaimTypes.NameIdentifier ("sub").
        string? sub = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out Guid id) ? id : Guid.Empty;
    }
}
