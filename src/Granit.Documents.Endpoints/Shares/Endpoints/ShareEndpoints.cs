using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Shares.Dtos;
using Granit.Documents.Endpoints.Shares.Mapping;
using Granit.Documents.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Endpoints.Shares.Endpoints;

/// <summary>
/// HTTP endpoints for ACL share grant / revoke / listing (F6.1).
/// </summary>
/// <remarks>
/// Five endpoints across three URL prefixes:
/// <list type="bullet">
///   <item><c>POST /folders/{id}/shares</c></item>
///   <item><c>GET  /folders/{id}/shares</c></item>
///   <item><c>POST /documents/{id}/shares</c></item>
///   <item><c>GET  /documents/{id}/shares</c></item>
///   <item><c>DELETE /shares/{id}</c></item>
/// </list>
/// </remarks>
internal static class ShareEndpoints
{
    private const string TagName = "Documents - Shares";

    public static RouteGroupBuilder MapShareEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Folder share endpoints — nested under /folders/{folderId}/shares.
        RouteGroupBuilder folderShares = group
            .MapGranitGroup("/folders/{folderId:guid}/shares")
            .WithTags(TagName);

        folderShares.MapGet("/", ListFolderSharesAsync)
            .WithName("ListFolderShares")
            .WithSummary("Lists active share grants on a folder.")
            .WithDescription(
                "Returns the share grants on the folder identified by `folderId`, ordered by "
                + "creation time. Expired grants are excluded. The path-based inheritance "
                + "(F6.4) is not flattened in this listing — callers see only grants directly "
                + "attached to this folder.")
            .RequireAuthorization(DocumentsPermissions.Shares.Read)
            .Produces<ListSharesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        folderShares.MapPost("/", GrantOnFolderAsync)
            .WithName("GrantFolderShare")
            .WithSummary("Grants a share on a folder.")
            .WithDescription(
                "Creates a `DocumentShare` row targeting the folder identified by `folderId`. "
                + "When `isDefault` is true (the default), the grant inherits to descendants "
                + "via the path-based effective-permission resolver introduced by F6.4 — F6.1 "
                + "only persists the flag.")
            .RequireAuthorization(DocumentsPermissions.Shares.Manage)
            .Produces<ShareResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        // Document share endpoints — nested under /documents/{documentId}/shares.
        RouteGroupBuilder documentShares = group
            .MapGranitGroup("/documents/{documentId:guid}/shares")
            .WithTags(TagName);

        documentShares.MapGet("/", ListDocumentSharesAsync)
            .WithName("ListDocumentShares")
            .WithSummary("Lists active share grants on a document.")
            .WithDescription(
                "Returns the share grants on the document identified by `documentId`, ordered "
                + "by creation time. Expired grants are excluded. Inherited grants from the "
                + "document's parent folder are not included — see the F6.5 effective ACL "
                + "field on document list responses for the resolved view.")
            .RequireAuthorization(DocumentsPermissions.Shares.Read)
            .Produces<ListSharesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        documentShares.MapPost("/", GrantOnDocumentAsync)
            .WithName("GrantDocumentShare")
            .WithSummary("Grants a share on a document.")
            .WithDescription(
                "Creates a `DocumentShare` row targeting the document identified by "
                + "`documentId`. The `isDefault` flag is ignored for document shares.")
            .RequireAuthorization(DocumentsPermissions.Shares.Manage)
            .Produces<ShareResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        // Top-level revoke endpoint — share id is globally unique inside a tenant.
        RouteGroupBuilder shares = group.MapGranitGroup("/shares").WithTags(TagName);

        shares.MapDelete("/{id:guid}", RevokeAsync)
            .WithName("RevokeShare")
            .WithSummary("Revokes a share grant.")
            .WithDescription(
                "Deletes the share row identified by `id` and emits `DocumentShareRevokedEvent` "
                + "for downstream cache invalidation (F6.3). Returns 404 when the share does "
                + "not exist or is excluded by the tenant filter.")
            .RequireAuthorization(DocumentsPermissions.Shares.Manage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<ListSharesResponse>, ProblemHttpResult>> ListFolderSharesAsync(
        Guid folderId,
        [FromServices] IDocumentShareService shares,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentShare> rows = await shares
            .ListForFolderAsync(folderId, cancellationToken)
            .ConfigureAwait(false);

        ShareResponse[] mapped = [.. rows.Select(s => s.ToResponse())];
        return TypedResults.Ok(new ListSharesResponse(mapped));
    }

    private static async Task<Results<Ok<ListSharesResponse>, ProblemHttpResult>> ListDocumentSharesAsync(
        Guid documentId,
        [FromServices] IDocumentShareService shares,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentShare> rows = await shares
            .ListForDocumentAsync(documentId, cancellationToken)
            .ConfigureAwait(false);

        ShareResponse[] mapped = [.. rows.Select(s => s.ToResponse())];
        return TypedResults.Ok(new ListSharesResponse(mapped));
    }

    private static async Task<Results<Created<ShareResponse>, ProblemHttpResult>> GrantOnFolderAsync(
        Guid folderId,
        GrantShareRequest request,
        [FromServices] IDocumentShareService shares,
        [FromServices] IHttpContextAccessor accessor,
        CancellationToken cancellationToken)
    {
        Guid createdByUserId = ResolveUserId(accessor.HttpContext);

        DocumentShare? share = await shares
            .GrantOnFolderAsync(
                folderId,
                request.GranteeType,
                request.GranteeId,
                request.Permission,
                request.IsDefault,
                createdByUserId,
                request.ExpiresAt,
                cancellationToken)
            .ConfigureAwait(false);
        if (share is null)
        {
            return TypedResults.Problem(
                $"Folder '{folderId}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Created($"/shares/{share.Id}", share.ToResponse());
    }

    private static async Task<Results<Created<ShareResponse>, ProblemHttpResult>> GrantOnDocumentAsync(
        Guid documentId,
        GrantShareRequest request,
        [FromServices] IDocumentShareService shares,
        [FromServices] IHttpContextAccessor accessor,
        CancellationToken cancellationToken)
    {
        Guid createdByUserId = ResolveUserId(accessor.HttpContext);

        DocumentShare? share = await shares
            .GrantOnDocumentAsync(
                documentId,
                request.GranteeType,
                request.GranteeId,
                request.Permission,
                createdByUserId,
                request.ExpiresAt,
                cancellationToken)
            .ConfigureAwait(false);
        if (share is null)
        {
            return TypedResults.Problem(
                $"Document '{documentId}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Created($"/shares/{share.Id}", share.ToResponse());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeAsync(
        Guid id,
        [FromServices] IDocumentShareService shares,
        CancellationToken cancellationToken)
    {
        bool removed = await shares.RevokeAsync(id, cancellationToken).ConfigureAwait(false);
        if (!removed)
        {
            return TypedResults.Problem(
                $"Share '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return TypedResults.NoContent();
    }

    private static Guid ResolveUserId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return Guid.Empty;
        }
        // The User aggregate id (ADR-051) is exposed on the principal as the standard
        // ClaimTypes.NameIdentifier ("sub"). Falling back to Guid.Empty keeps anonymous
        // flows (test harnesses) working; production deployments enforce authentication
        // via .RequireAuthorization() on the route group.
        string? sub = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out Guid id) ? id : Guid.Empty;
    }
}
