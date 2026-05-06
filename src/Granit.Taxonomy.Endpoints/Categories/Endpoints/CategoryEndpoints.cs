using System.Security.Claims;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Categories.Dtos;
using Granit.Taxonomy.Endpoints.Categories.Mapping;
using Granit.Taxonomy.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Taxonomy.Endpoints.Categories.Endpoints;

/// <summary>
/// HTTP endpoints for the hierarchical <see cref="Category"/> aggregate (T4.2):
/// CRUD, breadcrumb, move, and single-assignment endpoints.
/// </summary>
internal static class CategoryEndpoints
{
    private const string TagName = "Taxonomy";

    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder categories = group.MapGroup("/categories").WithTags(TagName);

        categories.MapGet("/", ListAsync)
            .WithName("ListTaxonomyCategories")
            .WithSummary("Lists categories under a parent (or roots) within a scope.")
            .WithDescription(
                "Returns the direct children of parentId in the given scope, ordered "
                + "by name. When parentId is omitted, returns the root categories of "
                + "the scope. Use GET /categories/{id} to fetch a single category "
                + "with its breadcrumb chain.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Read))
            .Produces<ListCategoriesResponse>()
            .ProducesValidationProblem();

        categories.MapGet("/{id:guid}", GetAsync)
            .WithName("GetTaxonomyCategory")
            .WithSummary("Returns a category with its breadcrumb chain.")
            .WithDescription(
                "Returns the category and its breadcrumb (root → leaf). Returns 404 "
                + "when the category is missing or excluded by the tenant filter.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Read))
            .Produces<CategoryDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPost("/", CreateAsync)
            .WithName("CreateTaxonomyCategory")
            .WithSummary("Creates a new category — root or under an existing parent.")
            .WithDescription(
                "Creates a new category in the supplied scope. When parentId is "
                + "supplied the new category is placed under it (must share scope). "
                + "Returns 422 when the parent does not exist or is in a different "
                + "scope, or when sibling-name uniqueness is violated.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Manage))
            .Produces<CategoryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        categories.MapPatch("/{id:guid}", UpdateAsync)
            .WithName("UpdateTaxonomyCategory")
            .WithSummary("Partially updates a category (rename / icon / hide).")
            .WithDescription(
                "Applies the supplied facets in turn. Renaming a category re-"
                + "materialises descendant paths via a single SQL UPDATE. Returns "
                + "404 when the category is missing.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Manage))
            .Produces<CategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        categories.MapPost("/{id:guid}/move", MoveAsync)
            .WithName("MoveTaxonomyCategory")
            .WithSummary("Moves a category under a new parent (or to root).")
            .WithDescription(
                "Re-parents the category and re-materialises descendant paths and "
                + "depths in a single SQL UPDATE. Returns 422 on cycle / cross-scope "
                + "violations, 404 when the category or target parent is missing.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Manage))
            .Produces<CategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        categories.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteTaxonomyCategory")
            .WithSummary("Hard-deletes a leaf category with no active assignments.")
            .WithDescription(
                "Returns 422 when the category has descendants OR when one or more "
                + "CategoryAssignment rows still reference it — callers must move / "
                + "delete descendants and reassign / unassign targets first.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Manage))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        categories.MapPost("/{id:guid}/assign", AssignAsync)
            .WithName("AssignTaxonomyCategory")
            .WithSummary("Sets the category for a target. Replaces any existing assignment.")
            .WithDescription(
                "Single-assignment per target: if a previous assignment exists for "
                + "(targetType, targetId), it is updated in place to point at the "
                + "supplied category. Returns 200 on update, 201 on first assignment.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Manage))
            .Produces<CategoryAssignmentResponse>(StatusCodes.Status200OK)
            .Produces<CategoryAssignmentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        categories.MapDelete("/assign/{targetType}/{targetId:guid}", UnassignAsync)
            .WithName("UnassignTaxonomyCategory")
            .WithSummary("Clears the category assignment for a target.")
            .WithDescription(
                "Removes the CategoryAssignment row for (targetType, targetId). "
                + "Returns 204 on success, 404 when no assignment matches.")
            .RequireAuthorization(p => p.RequireClaim("permission", TaxonomyPermissions.Categories.Manage))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<ListCategoriesResponse>, ValidationProblem>> ListAsync(
        [FromQuery] string scope,
        [FromQuery] Guid? parentId,
        [FromServices] ICategoryService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(scope)] = ["scope is required."],
            });
        }

        IReadOnlyList<Category> children = await service
            .ListChildrenAsync(scope, parentId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new ListCategoriesResponse(
            [.. children.Select(c => c.ToResponse())]));
    }

    private static async Task<Results<Ok<CategoryDetailResponse>, NotFound>> GetAsync(
        Guid id,
        [FromServices] ICategoryService service,
        CancellationToken cancellationToken)
    {
        Category? category = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        IReadOnlyList<Category> chain = await service
            .GetBreadcrumbAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new CategoryDetailResponse(
            category.ToResponse(),
            [.. chain.Select(c => c.ToResponse())]));
    }

    private static async Task<Results<Created<CategoryResponse>, ProblemHttpResult>> CreateAsync(
        CreateCategoryRequest request,
        [FromServices] ICategoryService service,
        CancellationToken cancellationToken)
    {
        try
        {
            Category category = await service
                .CreateAsync(
                    request.Scope,
                    request.ParentId,
                    request.Name,
                    request.IconName,
                    request.HideOnEntityCard ?? false,
                    cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Created($"/api/v1/taxonomy/categories/{category.Id}", category.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<Ok<CategoryResponse>, NotFound, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        [FromServices] ICategoryService service,
        CancellationToken cancellationToken)
    {
        Category? category = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            if (request.Name is not null)
            {
                category = await service.RenameAsync(id, request.Name, cancellationToken).ConfigureAwait(false);
            }
            if (request.IconName is not null && category is not null)
            {
                category = await service.SetIconAsync(id, request.IconName, cancellationToken).ConfigureAwait(false);
            }
            if (request.HideOnEntityCard is { } target && category is not null && category.HideOnEntityCard != target)
            {
                category = await service.ToggleHideAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return category is null ? TypedResults.NotFound() : TypedResults.Ok(category.ToResponse());
    }

    private static async Task<Results<Ok<CategoryResponse>, NotFound, ProblemHttpResult>> MoveAsync(
        Guid id,
        MoveCategoryRequest request,
        [FromServices] ICategoryService service,
        CancellationToken cancellationToken)
    {
        try
        {
            Category? moved = await service
                .MoveAsync(id, request.NewParentId, cancellationToken)
                .ConfigureAwait(false);
            return moved is null ? TypedResults.NotFound() : TypedResults.Ok(moved.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeleteAsync(
        Guid id,
        [FromServices] ICategoryService service,
        CancellationToken cancellationToken)
    {
        try
        {
            bool deleted = await service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<Ok<CategoryAssignmentResponse>, Created<CategoryAssignmentResponse>>> AssignAsync(
        Guid id,
        AssignCategoryRequest request,
        [FromServices] ICategoryAssignmentService service,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Guid userId = ExtractUserId(user);

        // Probe for an existing row to distinguish 200 vs 201.
        Category? before = await service
            .GetForTargetAsync(request.TargetType, request.TargetId, cancellationToken)
            .ConfigureAwait(false);

        Domain.CategoryAssignment assignment = await service
            .AssignAsync(id, request.TargetType, request.TargetId, userId, cancellationToken)
            .ConfigureAwait(false);

        return before is null
            ? TypedResults.Created(
                $"/api/v1/taxonomy/categories/assign/{request.TargetType}/{request.TargetId}",
                assignment.ToResponse())
            : TypedResults.Ok(assignment.ToResponse());
    }

    private static async Task<Results<NoContent, NotFound>> UnassignAsync(
        string targetType,
        Guid targetId,
        [FromServices] ICategoryAssignmentService service,
        CancellationToken cancellationToken)
    {
        bool removed = await service
            .UnassignAsync(targetType, targetId, cancellationToken)
            .ConfigureAwait(false);
        return removed ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static Guid ExtractUserId(ClaimsPrincipal user)
    {
        string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out Guid id) ? id : Guid.Empty;
    }
}
