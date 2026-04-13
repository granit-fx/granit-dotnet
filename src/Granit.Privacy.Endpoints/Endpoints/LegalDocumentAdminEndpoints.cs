using Granit.Guids;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Privacy.Endpoints.Endpoints;

internal static class LegalDocumentAdminEndpoints
{
    internal static RouteGroupBuilder MapLegalDocumentAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateAsync)
            .WithName("CreateLegalDocument")
            .WithSummary("Creates a new legal document draft.")
            .WithDescription(
                "Creates a new legal document version in Draft status. "
                + "The document can be edited and then published to become the active version. "
                + "Publishing auto-archives the previous active version.")
            .Produces<LegalDocumentDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(PrivacyPermissions.LegalDocuments.Create);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetLegalDocument")
            .WithSummary("Returns a legal document by ID, including drafts and archived versions.")
            .WithDescription(
                "Returns the full detail of a legal document version, regardless of its lifecycle status. "
                + "Requires admin read permission.")
            .Produces<LegalDocumentDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PrivacyPermissions.LegalDocuments.Read);

        group.MapGet("/", ListAsync)
            .WithName("ListLegalDocumentVersions")
            .WithSummary("Lists all versions of a legal document.")
            .WithDescription(
                "Returns the version history for the specified document ID, ordered by version descending. "
                + "Includes drafts, published, and archived versions.")
            .Produces<IReadOnlyList<LegalDocumentDetailResponse>>()
            .RequireAuthorization(PrivacyPermissions.LegalDocuments.Read);

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateLegalDocument")
            .WithSummary("Updates a legal document draft.")
            .WithDescription(
                "Updates metadata of a legal document in Draft status. "
                + "Returns 400 if the document is not in Draft status.")
            .Produces<LegalDocumentDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .RequireAuthorization(PrivacyPermissions.LegalDocuments.Manage);

        group.MapPost("/{id:guid}/publish", PublishAsync)
            .WithName("PublishLegalDocument")
            .WithSummary("Publishes a legal document draft, auto-archiving the previous published version.")
            .WithDescription(
                "Promotes the draft to Published status. If a published version already exists "
                + "for the same document ID, it is archived in the same transaction and "
                + "LegalAgreementObsoleteEto is dispatched to trigger re-consent flows.")
            .Produces<LegalDocumentDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PrivacyPermissions.LegalDocuments.Manage);

        return group;
    }

    private static async Task<Created<LegalDocumentDetailResponse>> CreateAsync(
        LegalDocumentCreateRequest request,
        [FromServices] ILegalDocumentWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        var document = LegalDocument.Create(
            guidGenerator.Create(),
            request.DocumentId,
            request.DisplayName,
            request.Description,
            request.TemplateName);

        await writer.InsertAsync(document, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/legal-documents/{document.Id}",
            ToResponse(document));
    }

    private static async Task<Results<Ok<LegalDocumentDetailResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] ILegalDocumentReader reader,
        CancellationToken cancellationToken)
    {
        LegalDocument? document = await reader.GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return document is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(ToResponse(document));
    }

    private static async Task<Ok<IReadOnlyList<LegalDocumentDetailResponse>>> ListAsync(
        [FromQuery] string documentId,
        [FromServices] ILegalDocumentReader reader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LegalDocument> documents = await reader
            .GetVersionHistoryAsync(documentId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LegalDocumentDetailResponse> response = documents
            .Select(ToResponse).ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<LegalDocumentDetailResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        LegalDocumentUpdateRequest request,
        [FromServices] ILegalDocumentReader reader,
        [FromServices] ILegalDocumentWriter writer,
        CancellationToken cancellationToken)
    {
        LegalDocument? document = await reader.GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            document.UpdateDraft(
                request.DisplayName,
                request.Description,
                request.TemplateName,
                request.DocumentBlobId);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        await writer.UpdateAsync(document, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(ToResponse(document));
    }

    private static async Task<Results<Ok<LegalDocumentDetailResponse>, ProblemHttpResult>> PublishAsync(
        Guid id,
        [FromServices] ILegalDocumentPublicationService publicationService,
        CancellationToken cancellationToken)
    {
        try
        {
            LegalDocument published = await publicationService
                .PublishAsync(id, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(ToResponse(published));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static LegalDocumentDetailResponse ToResponse(LegalDocument doc) =>
        new(doc.Id,
            doc.DocumentId,
            doc.Version,
            doc.LifecycleStatus.ToString(),
            doc.DisplayName,
            doc.Description,
            doc.TemplateName,
            doc.DocumentBlobId,
            doc.CreatedAt,
            doc.ModifiedAt ?? doc.CreatedAt);
}
