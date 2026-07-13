using Granit.Guids;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.Privacy.LegalAgreements.Exceptions;
using Granit.QueryEngine.Endpoints.Extensions;
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
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
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

        // GET / and GET /meta — served by the query engine (filtering, sorting, search, pagination).
        // GET /        → PagedResult<LegalDocumentDetailResponse>
        // GET /meta    → query metadata (columns, filters, sorts, presets)
        // The "published" quick filter is the default; pass ?quickFilters=draft to see drafts.
        // Filter by document ID via ?filter[documentId.eq]=privacy-policy to view version history.
        // Cross-tenant reads are fail-closed by default: a host operator with no resolved tenant
        // sees only the host partition. To expose cross-tenant legal-document visibility, mark this
        // route .AllowHostAccess(); a platform admin holding LegalDocuments.Read at global scope then
        // reads across tenants, while the multi-tenant filter stays enforced for tenant callers.
        group.MapGranitQuery<LegalDocument>(configure: opts =>
            opts.AuthorizationPolicy = PrivacyPermissions.LegalDocuments.Read);

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateLegalDocument")
            .WithSummary("Updates a legal document draft.")
            .WithDescription(
                "Updates metadata of a legal document in Draft status. "
                + "Returns 400 if the document is not in Draft status. "
                + "Returns 409 if the concurrency stamp does not match the stored value.")
            .Produces<LegalDocumentDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
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

        await writer.UpdateAsync(document, request.ConcurrencyStamp, cancellationToken).ConfigureAwait(false);

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
        catch (LegalDocumentNotFoundException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        catch (LegalDocumentNotPublishableException ex)
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
            doc.ModifiedAt ?? doc.CreatedAt,
            doc.ConcurrencyStamp);
}
