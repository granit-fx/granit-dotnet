using Granit.Exceptions;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.Layouts;
using Granit.Templating.Store;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating.Endpoints.Endpoints;

/// <summary>
/// Category and layout Minimal API endpoints for templates:
/// CRUD for categories + available layout names.
/// </summary>
internal static class TemplatingCategoryEndpoints
{
    /// <summary>Maps all template category and layout endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapTemplatingCategoryEndpoints(this RouteGroupBuilder group)
    {
        // ----- Layouts — Read (Templates.Read) -----

        group.MapGet("/layouts", HandleListLayoutsAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("ListAvailableLayouts")
             .WithSummary("Returns all available layout template names.")
             .WithDescription("Returns a deduplicated list of layout template names from both the code-level ILayoutRegistry and database-assigned LayoutName values. Used by the admin UI to populate layout dropdowns.")
             .Produces<IReadOnlyList<string>>();

        // ----- Categories — Read (Categories.Read) -----

        group.MapGet("/categories", HandleListCategoriesAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Read)
             .WithName("ListTemplateCategories")
             .WithSummary("Returns all template categories ordered by sort order then name.")
             .WithDescription("Returns all template categories for organizing templates. Categories are sorted by their sort order, then alphabetically by name. Each category includes its ID, name, and template count.")
             .Produces<IReadOnlyList<TemplateCategoryResponse>>()
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        // ----- Categories — Write (Categories.Manage) -----

        group.MapPost("/categories", HandleCreateCategoryAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Manage)
             .WithName("CreateTemplateCategory")
             .WithSummary("Creates a new template category.")
             .WithDescription("Creates a new template category with the given name and sort order. The name must be unique.")
             .Produces<TemplateCategoryResponse>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPut("/categories/{id:guid}", HandleUpdateCategoryAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Manage)
             .WithName("UpdateTemplateCategory")
             .WithSummary("Updates an existing template category.")
             .WithDescription("Updates the name and sort order of an existing category. Returns 404 if the category does not exist.")
             .Produces<TemplateCategoryResponse>()
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapDelete("/categories/{id:guid}", HandleDeleteCategoryAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Manage)
             .WithName("DeleteTemplateCategory")
             .WithSummary("Deletes a template category (409 if templates are still associated).")
             .WithDescription("Deletes the template category. Returns 409 Conflict if templates are still associated with this category — reassign or delete them first. Returns 404 if the category does not exist.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    // -------------------------------------------------------------------------
    // GET /layouts — List available layouts
    // -------------------------------------------------------------------------

    private static async Task<Ok<IReadOnlyList<string>>> HandleListLayoutsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ILayoutRegistry? layoutRegistry = context.RequestServices.GetService<ILayoutRegistry>();
        IDocumentTemplateStoreReader? storeReader = context.RequestServices.GetService<IDocumentTemplateStoreReader>();

        // Merge layout names from code registry + DB store, deduplicated
        HashSet<string> layouts = new(layoutRegistry?.GetAllLayoutNames() ?? [], StringComparer.Ordinal);

        if (storeReader is not null)
        {
            IReadOnlyList<string> dbLayouts = await storeReader
                .GetDistinctLayoutNamesAsync(cancellationToken).ConfigureAwait(false);

            foreach (string layoutName in dbLayouts)
            {
                layouts.Add(layoutName);
            }
        }

        IReadOnlyList<string> result = layouts.Order(StringComparer.Ordinal).ToList();
        return TypedResults.Ok(result);
    }

    // -------------------------------------------------------------------------
    // GET /categories — List all categories
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<IReadOnlyList<TemplateCategoryResponse>>, ProblemHttpResult>> HandleListCategoriesAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreReader? storeReader =
            context.RequestServices.GetService<ITemplateCategoryStoreReader>();

        if (storeReader is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        IReadOnlyList<TemplateCategory> categories =
            await storeReader.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<TemplateCategoryResponse> response = categories
            .Select(TemplatingResponseMapper.ToCategoryResponse)
            .ToList();

        return TypedResults.Ok(response);
    }

    // -------------------------------------------------------------------------
    // POST /categories — Create a category
    // -------------------------------------------------------------------------

    private static async Task<Results<Created<TemplateCategoryResponse>, ProblemHttpResult>> HandleCreateCategoryAsync(
        HttpContext context,
        SaveTemplateCategoryRequest body,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreWriter? storeWriter =
            context.RequestServices.GetService<ITemplateCategoryStoreWriter>();

        if (storeWriter is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        string userId = GetCurrentUserId(context);

        TemplateCategory category;
        try
        {
            category = await storeWriter.CreateCategoryAsync(
                body.Name, body.Description, body.Icon, body.SortOrder, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Created($"categories/{category.Id}", TemplatingResponseMapper.ToCategoryResponse(category));
    }

    // -------------------------------------------------------------------------
    // PUT /categories/{id} — Update a category
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateCategoryResponse>, ProblemHttpResult>> HandleUpdateCategoryAsync(
        HttpContext context,
        Guid id,
        SaveTemplateCategoryRequest body,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreWriter? storeWriter =
            context.RequestServices.GetService<ITemplateCategoryStoreWriter>();

        if (storeWriter is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        TemplateCategory category;
        try
        {
            category = await storeWriter.UpdateCategoryAsync(
                id, body.Name, body.Description, body.Icon, body.SortOrder, cancellationToken).ConfigureAwait(false);
        }
        catch (EntityNotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Ok(TemplatingResponseMapper.ToCategoryResponse(category));
    }

    // -------------------------------------------------------------------------
    // DELETE /categories/{id} — Delete a category
    // -------------------------------------------------------------------------

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeleteCategoryAsync(
        HttpContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreWriter? storeWriter =
            context.RequestServices.GetService<ITemplateCategoryStoreWriter>();

        if (storeWriter is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        try
        {
            await storeWriter.DeleteCategoryAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (EntityNotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // Shared helper
    // -------------------------------------------------------------------------

    private static string GetCurrentUserId(HttpContext context)
    {
        ICurrentUserService? userService = context.RequestServices.GetService<ICurrentUserService>();
        return userService?.UserId
            ?? userService?.UserName
            ?? throw new UnauthorizedAccessException("Unable to resolve current user identity for audit trail.");
    }
}
