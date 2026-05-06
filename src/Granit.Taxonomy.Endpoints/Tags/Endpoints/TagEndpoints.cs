using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Permissions;
using Granit.Taxonomy.Endpoints.Tags.Dtos;
using Granit.Taxonomy.Endpoints.Tags.Mapping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Taxonomy.Endpoints.Tags.Endpoints;

/// <summary>
/// HTTP endpoints for the Tag CRUD surface plus per-scope autocomplete.
/// </summary>
internal static class TagEndpoints
{
    private const string TagName = "Taxonomy";

    public static RouteGroupBuilder MapTagEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder tags = group.MapGroup("/tags").WithTags(TagName);

        tags.MapGet("/", ListTagsAsync)
            .WithName("ListTaxonomyTags")
            .WithSummary("Lists tags in a scope, optionally filtered by a name prefix.")
            .WithDescription(
                "Returns the tenant-scoped tags registered under the given scope, ordered "
                + "by name. The scope query parameter is mandatory; cross-scope search is "
                + "exposed by the dedicated /api/v1/taxonomy/search endpoint introduced in "
                + "story T3.1. The optional q parameter applies a case-insensitive name "
                + "prefix filter for autocomplete-style lookups.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Read))
            .Produces<ListTagsResponse>()
            .ProducesValidationProblem();

        tags.MapGet("/{id:guid}", GetTagAsync)
            .WithName("GetTaxonomyTag")
            .WithSummary("Returns a tag by id.")
            .WithDescription(
                "Returns the tag identified by the route id. Returns 404 when the tag is "
                + "missing or excluded by the tenant filter.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Read))
            .Produces<TagResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        tags.MapPost("/", CreateTagAsync)
            .WithName("CreateTaxonomyTag")
            .WithSummary("Creates a new tag in the current tenant.")
            .WithDescription(
                "Creates a new tag in the supplied scope. Returns 201 with the persisted "
                + "tag. Returns 422 when a tag with the same (TenantId, Scope, Name) "
                + "already exists.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Manage))
            .Produces<TagResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        tags.MapPatch("/{id:guid}", UpdateTagAsync)
            .WithName("UpdateTaxonomyTag")
            .WithSummary("Partially updates a tag (name / colour / hide-on-entity-card).")
            .WithDescription(
                "Applies the supplied facets in turn. Each property is independently "
                + "optional; an empty body is a no-op. Returns 404 when the tag is "
                + "missing, 422 when a rename collides with an existing (TenantId, Scope, "
                + "Name) row.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Manage))
            .Produces<TagResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        tags.MapDelete("/{id:guid}", DeleteTagAsync)
            .WithName("DeleteTaxonomyTag")
            .WithSummary("Hard-deletes a tag.")
            .WithDescription(
                "Removes the tag row. Cascading cleanup of orphan TagAssignment rows is "
                + "handled by T5.1's EntityDeletedEto listener. Returns 404 when the tag "
                + "is missing.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Tags.Manage))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<ListTagsResponse>, ValidationProblem>> ListTagsAsync(
        [FromQuery] string scope,
        [FromQuery] string? q,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        [FromServices] ITagService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(scope)] = ["scope is required."],
            });
        }

        IReadOnlyList<Tag> tags = await service
            .ListByScopeAsync(scope, q, skip ?? 0, take ?? 50, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new ListTagsResponse([.. tags.Select(t => t.ToResponse())]));
    }

    private static async Task<Results<Ok<TagResponse>, NotFound>> GetTagAsync(
        Guid id,
        [FromServices] ITagService service,
        CancellationToken cancellationToken)
    {
        Tag? tag = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return tag is null ? TypedResults.NotFound() : TypedResults.Ok(tag.ToResponse());
    }

    private static async Task<Results<Created<TagResponse>, ProblemHttpResult>> CreateTagAsync(
        CreateTagRequest request,
        [FromServices] ITagService service,
        CancellationToken cancellationToken)
    {
        try
        {
            Tag tag = await service
                .CreateAsync(
                    request.Scope,
                    request.Name,
                    request.Color,
                    request.HideOnEntityCard ?? false,
                    cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Created($"/api/v1/taxonomy/tags/{tag.Id}", tag.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<Ok<TagResponse>, NotFound, ProblemHttpResult>> UpdateTagAsync(
        Guid id,
        UpdateTagRequest request,
        [FromServices] ITagService service,
        CancellationToken cancellationToken)
    {
        Tag? tag = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (tag is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            if (request.Name is not null)
            {
                tag = await service.RenameAsync(id, request.Name, cancellationToken).ConfigureAwait(false);
            }
            if (request.Color is not null)
            {
                tag = await service.RecolourAsync(id, request.Color, cancellationToken).ConfigureAwait(false);
            }
            if (request.HideOnEntityCard is { } target && tag is not null && tag.HideOnEntityCard != target)
            {
                tag = await service.ToggleHideAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return tag is null ? TypedResults.NotFound() : TypedResults.Ok(tag.ToResponse());
    }

    private static async Task<Results<NoContent, NotFound>> DeleteTagAsync(
        Guid id,
        [FromServices] ITagService service,
        CancellationToken cancellationToken)
    {
        bool deleted = await service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
