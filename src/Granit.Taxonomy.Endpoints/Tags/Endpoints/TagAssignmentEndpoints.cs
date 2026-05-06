using System.Security.Claims;
using Granit.Taxonomy.Authorization;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Permissions;
using Granit.Taxonomy.Endpoints.Tags.Dtos;
using Granit.Taxonomy.Endpoints.Tags.Mapping;
using Granit.Taxonomy.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Taxonomy.Endpoints.Tags.Endpoints;

/// <summary>
/// HTTP endpoints for the polymorphic <c>TagAssignment</c> table introduced by ADR-054
/// (T2.2): idempotent assignment, idempotent unassignment, and per-target listing.
/// </summary>
internal static class TagAssignmentEndpoints
{
    private const string TagName = "Taxonomy";

    public static RouteGroupBuilder MapTagAssignmentEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Assign / unassign live under /tags/{id}/...
        RouteGroupBuilder tags = group.MapGroup("/tags/{id:guid}/assign").WithTags(TagName);

        tags.MapPost("/", AssignAsync)
            .WithName("AssignTaxonomyTag")
            .WithSummary("Idempotently assigns a tag to a target aggregate.")
            .WithDescription(
                "Creates a new TagAssignment row for the given (TagId, TargetType, "
                + "TargetId) triplet. When the row already exists, the existing row is "
                + "returned with status 200 instead of 201 — the operation is "
                + "idempotent. Returns 422 when targetType is not a registered "
                + "taggable aggregate; 403 when the per-target permission check fails.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Manage))
            .Produces<TagAssignmentResponse>(StatusCodes.Status201Created)
            .Produces<TagAssignmentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        tags.MapDelete("/{targetType}/{targetId:guid}", UnassignAsync)
            .WithName("UnassignTaxonomyTag")
            .WithSummary("Removes a tag assignment.")
            .WithDescription(
                "Deletes the TagAssignment row matching the (TagId, TargetType, "
                + "TargetId) triplet. Returns 204 on success, 404 when no matching "
                + "row exists, 403 when the per-target permission check fails.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Manage))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // /assignments — per-target listing
        RouteGroupBuilder assignments = group.MapGroup("/assignments").WithTags(TagName);

        assignments.MapGet("/", ListForTargetAsync)
            .WithName("ListTaxonomyAssignments")
            .WithSummary("Lists tags currently assigned to a target.")
            .WithDescription(
                "Returns every Tag assigned to (targetType, targetId), ordered by tag "
                + "name. Both query parameters are mandatory.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Read))
            .Produces<ListTagsResponse>()
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Results<Created<TagAssignmentResponse>, Ok<TagAssignmentResponse>, ProblemHttpResult, ForbidHttpResult>> AssignAsync(
        Guid id,
        AssignTagRequest request,
        [FromServices] ITagAssignmentService service,
        [FromServices] TaggableTypeRegistry registry,
        [FromServices] ITaggablePermissionResolver permissionResolver,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!registry.IsRegistered(request.TargetType))
        {
            return TypedResults.Problem(
                $"Target type '{request.TargetType}' is not a registered taggable aggregate.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        bool authorized = await permissionResolver
            .IsAuthorizedAsync(request.TargetType, request.TargetId, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            return TypedResults.Forbid();
        }

        Guid userId = ExtractUserId(user);

        (TagAssignment assignment, bool created) = await service
            .AssignAsync(id, request.TargetType, request.TargetId, userId, cancellationToken)
            .ConfigureAwait(false);

        TagAssignmentResponse response = assignment.ToResponse();
        return created
            ? TypedResults.Created($"/api/v1/taxonomy/tags/{id}/assign/{request.TargetType}/{request.TargetId}", response)
            : TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> UnassignAsync(
        Guid id,
        string targetType,
        Guid targetId,
        [FromServices] ITagAssignmentService service,
        [FromServices] ITaggablePermissionResolver permissionResolver,
        CancellationToken cancellationToken)
    {
        bool authorized = await permissionResolver
            .IsAuthorizedAsync(targetType, targetId, cancellationToken)
            .ConfigureAwait(false);
        if (!authorized)
        {
            return TypedResults.Forbid();
        }

        bool deleted = await service
            .UnassignAsync(id, targetType, targetId, cancellationToken)
            .ConfigureAwait(false);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<ListTagsResponse>, ValidationProblem>> ListForTargetAsync(
        [FromQuery] string targetType,
        [FromQuery] Guid targetId,
        [FromServices] ITagAssignmentService service,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new();
        if (string.IsNullOrWhiteSpace(targetType))
        {
            errors[nameof(targetType)] = ["targetType is required."];
        }
        if (targetId == Guid.Empty)
        {
            errors[nameof(targetId)] = ["targetId is required."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        IReadOnlyList<Tag> tags = await service
            .ListForTargetAsync(targetType, targetId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new ListTagsResponse([.. tags.Select(t => t.ToResponse())]));
    }

    private static Guid ExtractUserId(ClaimsPrincipal user)
    {
        string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out Guid id) ? id : Guid.Empty;
    }
}
