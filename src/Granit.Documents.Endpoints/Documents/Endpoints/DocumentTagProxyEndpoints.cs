using System.Security.Claims;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;
using Granit.Documents.Permissions;
using Granit.Taxonomy;
using Granit.Taxonomy.Domain;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Endpoints.Documents.Endpoints;

/// <summary>
/// Proxy endpoints for the per-document tag surface (T6.1). Pure UX shortcut —
/// every operation delegates to <see cref="ITagAssignmentService"/> with the
/// canonical <c>TargetType = typeof(Document).FullName</c>. The Taxonomy module
/// remains the source of truth.
/// </summary>
internal static class DocumentTagProxyEndpoints
{
    private const string TagName = "Documents";
    private static readonly string DocumentTargetType = typeof(Document).FullName!;

    public static RouteGroupBuilder MapDocumentTagProxyEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder tags = group.MapGranitGroup("/documents/{id:guid}/tags").WithTags(TagName);

        tags.MapGet("/", ListTagsAsync)
            .WithName("ListDocumentTags")
            .WithSummary("Lists every tag currently assigned to a document.")
            .WithDescription(
                "Proxy over Granit.Taxonomy: returns the tags assigned to the document "
                + "with TargetType = Granit.Documents.Domain.Document. The canonical store "
                + "lives in Taxonomy; this endpoint is purely a UX shortcut on the "
                + "Documents surface. Permission: Documents.Documents.Read.")
            .RequireAuthorization(DocumentsPermissions.Documents.Read)
            .Produces<ListDocumentTagsResponse>();

        tags.MapPost("/{tagId:guid}", AssignTagAsync)
            .WithName("AssignDocumentTag")
            .WithSummary("Assigns a tag to a document.")
            .WithDescription(
                "Idempotent assignment: re-posting the same (document, tag) pair returns the "
                + "existing row instead of creating a duplicate. Delegates to "
                + "Granit.Taxonomy's TagAssignmentService. Permission: Documents.Documents.Manage.")
            .RequireAuthorization(DocumentsPermissions.Documents.Manage)
            .Produces<DocumentTagAssignmentResponse>(StatusCodes.Status201Created)
            .Produces<DocumentTagAssignmentResponse>(StatusCodes.Status200OK);

        tags.MapDelete("/{tagId:guid}", UnassignTagAsync)
            .WithName("UnassignDocumentTag")
            .WithSummary("Removes a tag assignment from a document.")
            .WithDescription(
                "Deletes the (document, tag) assignment row. Returns 404 when no row matches "
                + "(typically because the tag was never assigned, or has already been removed). "
                + "Permission: Documents.Documents.Manage.")
            .RequireAuthorization(DocumentsPermissions.Documents.Manage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<ListDocumentTagsResponse>> ListTagsAsync(
        Guid id,
        [FromServices] ITagAssignmentService service,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Tag> tags = await service
            .ListForTargetAsync(DocumentTargetType, id, cancellationToken)
            .ConfigureAwait(false);

        DocumentTagResponse[] items = [.. tags.Select(t =>
            new DocumentTagResponse(t.Id, t.TenantId, t.Scope, t.Name, t.Color, t.HideOnEntityCard, t.RowVersion))];
        return TypedResults.Ok(new ListDocumentTagsResponse(items));
    }

    private static async Task<Results<Created<DocumentTagAssignmentResponse>, Ok<DocumentTagAssignmentResponse>>> AssignTagAsync(
        Guid id,
        Guid tagId,
        [FromServices] ITagAssignmentService service,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Guid userId = ExtractUserId(user);

        (TagAssignment assignment, bool created) = await service
            .AssignAsync(tagId, DocumentTargetType, id, userId, cancellationToken)
            .ConfigureAwait(false);

        DocumentTagAssignmentResponse response = new(
            assignment.Id,
            assignment.TenantId,
            assignment.TagId,
            assignment.TargetId,
            assignment.AssignedAt,
            assignment.AssignedByUserId);

        return created
            ? TypedResults.Created($"/api/v1/documents/{id}/tags/{tagId}", response)
            : TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UnassignTagAsync(
        Guid id,
        Guid tagId,
        [FromServices] ITagAssignmentService service,
        CancellationToken cancellationToken)
    {
        bool deleted = await service
            .UnassignAsync(tagId, DocumentTargetType, id, cancellationToken)
            .ConfigureAwait(false);
        return deleted
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                detail: $"Tag assignment '{tagId}' on document '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
    }

    private static Guid ExtractUserId(ClaimsPrincipal user)
    {
        string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out Guid id) ? id : Guid.Empty;
    }
}
