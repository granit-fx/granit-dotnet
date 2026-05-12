using Granit.Documents.Authorization;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;
using Granit.Documents.Endpoints.Documents.Mapping;
using Granit.Documents.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Endpoints.Documents.Endpoints;

/// <summary>
/// HTTP endpoints for document lifecycle mutations (F3.4): get-by-id, rename / update
/// description, move, trash.
/// </summary>
internal static class DocumentMutationEndpoints
{
    public static RouteGroupBuilder MapDocumentMutationEndpoints(this RouteGroupBuilder documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        documents.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetDocument")
            .WithSummary("Returns a document by id.")
            .WithDescription(
                "Returns the document identified by `id`, including its current version "
                + "pointer and folder placement. 404 when the document is not found or "
                + "excluded by the tenant filter.")
            .RequireAuthorization(DocumentsPermissions.Documents.Read)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        documents.MapPatch("/{id:guid}", RenameAsync)
            .WithName("RenameDocument")
            .WithSummary("Renames a document and / or updates its description.")
            .WithDescription(
                "Partial update — fields left null are unchanged. Send `clearDescription: true` "
                + "to drop the description (sending an empty string sets a non-null empty "
                + "value, which is rarely what callers want). 404 when the document is "
                + "missing; 409 when the document is trashed.")
            .RequireAuthorization(DocumentsPermissions.Documents.Manage)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        documents.MapPost("/{id:guid}/move", MoveAsync)
            .WithName("MoveDocument")
            .WithSummary("Moves a document under a different folder.")
            .WithDescription(
                "Moves the document identified by `id` under `newFolderId` (or under the "
                + "tenant root when omitted). Cross-tenant moves and moves into trashed or "
                + "missing folders surface as 409 Conflict.")
            .RequireAuthorization(DocumentsPermissions.Documents.Manage)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        documents.MapDelete("/{id:guid}", TrashAsync)
            .WithName("TrashDocument")
            .WithSummary("Sends a document to the trash (soft-delete).")
            .WithDescription(
                "Moves the document identified by `id` to the trash. Permanent deletion "
                + "happens after the configured retention period via the empty-trash "
                + "background job (F8 / F9.2). Already-trashed documents surface as 409.")
            .RequireAuthorization(DocumentsPermissions.Documents.Manage)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        documents.MapPost("/{id:guid}/restore", RestoreAsync)
            .WithName("RestoreDocument")
            .WithSummary("Restores a trashed document.")
            .WithDescription(
                "Sets the document identified by `id` back to `Active`. Returns 404 when "
                + "the document is missing or not currently trashed; 409 when the parent "
                + "folder is itself trashed (callers must restore the folder first).")
            .RequireAuthorization(DocumentsPermissions.Documents.Manage)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        documents.MapDelete("/{id:guid}/permanent", PermanentlyDeleteAsync)
            .WithName("PermanentlyDeleteDocument")
            .WithSummary("Permanently deletes a trashed document (F8.2).")
            .WithDescription(
                "Soft-deletes every version's `BlobDescriptor` (the bytes are removed; "
                + "the audit row is retained per `Granit.BlobStorage`'s 3-year retention), "
                + "decrements the tenant's storage quota by the released bytes, and "
                + "promotes the document to `Status = PermanentlyDeleted` (tombstone for "
                + "the GDPR / ISO 27001 audit trail). Emits `DocumentPermanentlyDeletedEvent`. "
                + "Returns 204 on success, 404 when the document is missing or not "
                + "currently trashed.")
            .RequireAuthorization(DocumentsPermissions.Documents.Manage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return documents;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<DocumentResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IDocumentService documents,
        [FromServices] IDocumentPrincipalAccessor principalAccessor,
        [FromServices] IEffectivePermissionResolver permissionResolver,
        CancellationToken cancellationToken)
    {
        Document? document = await documents.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return TypedResults.Problem(
                $"Document '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        EffectivePermissionLevel? permission = null;
        DocumentPrincipal? principal = principalAccessor.GetCurrent();
        if (principal is not null)
        {
            permission = await permissionResolver
                .GetDocumentPermissionAsync(document.Id, principal, cancellationToken)
                .ConfigureAwait(false);
        }
        return TypedResults.Ok(document.ToResponse(permission));
    }

    private static async Task<Results<Ok<DocumentResponse>, ProblemHttpResult>> RenameAsync(
        Guid id,
        RenameDocumentRequest request,
        [FromServices] IDocumentService documents,
        CancellationToken cancellationToken)
    {
        try
        {
            Document? document = null;
            if (request.Name is not null)
            {
                document = await documents.RenameAsync(id, request.Name, cancellationToken).ConfigureAwait(false);
                if (document is null)
                {
                    return TypedResults.Problem(
                        $"Document '{id}' was not found.",
                        statusCode: StatusCodes.Status404NotFound);
                }
            }

            if (request.ClearDescription || request.Description is not null)
            {
                string? newDescription = request.ClearDescription ? null : request.Description;
                document = await documents.UpdateDescriptionAsync(id, newDescription, cancellationToken)
                    .ConfigureAwait(false);
                if (document is null)
                {
                    return TypedResults.Problem(
                        $"Document '{id}' was not found.",
                        statusCode: StatusCodes.Status404NotFound);
                }
            }

            // Validator guarantees at least one mutation requested; document is not null here.
            return TypedResults.Ok(document!.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<Ok<DocumentResponse>, ProblemHttpResult>> MoveAsync(
        Guid id,
        MoveDocumentRequest request,
        [FromServices] IDocumentService documents,
        CancellationToken cancellationToken)
    {
        try
        {
            Document? document = await documents
                .MoveAsync(id, request.NewFolderId, cancellationToken)
                .ConfigureAwait(false);
            if (document is null)
            {
                return TypedResults.Problem(
                    $"Document '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return TypedResults.Ok(document.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<Ok<DocumentResponse>, ProblemHttpResult>> TrashAsync(
        Guid id,
        [FromServices] IDocumentService documents,
        CancellationToken cancellationToken)
    {
        try
        {
            Document? document = await documents.TrashAsync(id, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return TypedResults.Problem(
                    $"Document '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return TypedResults.Ok(document.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PermanentlyDeleteAsync(
        Guid id,
        [FromServices] IDocumentService documents,
        CancellationToken cancellationToken)
    {
        Document? document = await documents
            .PermanentlyDeleteAsync(id, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return TypedResults.Problem(
                $"Document '{id}' was not found or is not currently trashed.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<DocumentResponse>, ProblemHttpResult>> RestoreAsync(
        Guid id,
        [FromServices] IDocumentService documents,
        CancellationToken cancellationToken)
    {
        try
        {
            Document? document = await documents.RestoreAsync(id, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return TypedResults.Problem(
                    $"Document '{id}' was not found or is not currently trashed.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return TypedResults.Ok(document.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            // Parent folder is trashed.
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
