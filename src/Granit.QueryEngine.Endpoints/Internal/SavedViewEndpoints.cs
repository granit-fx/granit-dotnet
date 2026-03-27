using System.Security.Claims;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine.Endpoints.Dtos;
using Granit.QueryEngine.Options;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.QueryEngine.Endpoints.Internal;

/// <summary>
/// CRUD endpoints for saved views.
/// </summary>
internal static class SavedViewEndpoints
{
    /// <summary>
    /// Registers saved view endpoints on the route group:
    /// GET /saved-views, POST /saved-views, PUT /saved-views/{id},
    /// DELETE /saved-views/{id}, POST /saved-views/{id}/set-default.
    /// </summary>
    internal static void MapSavedViewEndpoints(
        this RouteGroupBuilder group, string entityType)
    {
        RouteGroupBuilder savedViews = group.MapGroup("/saved-views");

        savedViews.MapGet("/", (
            [FromServices] ISavedViewStoreReader store,
            [FromServices] ICurrentTenant tenant,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            GetListAsync(store, entityType, tenant, user, cancellationToken))
            .WithName($"GetSavedViews_{entityType}")
            .WithSummary("Returns saved views for the current user.")
            .WithDescription("Returns all saved views owned by the authenticated user for this entity type within the current tenant. Each view contains its filter, sort, column selection, and whether it is the user's default view.")
            .Produces<List<SavedViewResponse>>();

        savedViews.MapPost("/", (
            CreateSavedViewRequest request,
            [FromServices] ISavedViewStoreReader reader,
            [FromServices] ISavedViewStoreWriter store,
            [FromServices] IGuidGenerator guidGenerator,
            [FromServices] IOptions<QueryEngineOptions> engineOptions,
            [AsParameters] SavedViewUserContext ctx,
            CancellationToken cancellationToken) =>
            CreateAsync(request, reader, store, guidGenerator, engineOptions, entityType, ctx, cancellationToken))
            .WithName($"CreateSavedView_{entityType}")
            .WithSummary("Creates a new saved view.")
            .WithDescription("Creates a new saved view for the current user and entity type. The view stores a reusable query configuration (filters, sort, column selection). Returns 201 Created with the saved view details.")
            .Produces<SavedViewResponse>(StatusCodes.Status201Created);

        savedViews.MapPut("/{id:guid}", (
            Guid id,
            UpdateSavedViewRequest request,
            [FromServices] ISavedViewStoreReader reader,
            [FromServices] ISavedViewStoreWriter writer,
            [FromServices] IClock clock,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            UpdateAsync(id, request, reader, writer, clock, user, cancellationToken))
            .WithName($"UpdateSavedView_{entityType}")
            .WithSummary("Updates an existing saved view.")
            .WithDescription("Replaces the name, filter, sort, and column selection of an existing saved view. Returns 404 if the view does not exist. Returns 403 if the view belongs to another user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        savedViews.MapDelete("/{id:guid}", (
            Guid id,
            [FromServices] ISavedViewStoreReader reader,
            [FromServices] ISavedViewStoreWriter store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            DeleteAsync(id, reader, store, user, cancellationToken))
            .WithName($"DeleteSavedView_{entityType}")
            .WithSummary("Deletes a saved view.")
            .WithDescription("Permanently deletes the saved view. If it was the user's default view, no default is set afterward. Returns 404 if the view does not exist. Returns 403 if the view belongs to another user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        savedViews.MapPost("/{id:guid}/set-default", (
            Guid id,
            [FromServices] ISavedViewStoreReader reader,
            [FromServices] ISavedViewStoreWriter store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            SetDefaultAsync(id, reader, store, entityType, user, cancellationToken))
            .WithName($"SetDefaultSavedView_{entityType}")
            .WithSummary("Sets a saved view as the default for the current user.")
            .WithDescription("Marks the specified saved view as the user's default for this entity type. The previous default (if any) is unset. The view must belong to the current user or be shared. Returns 404 if the view does not exist. Returns 403 if the view belongs to another user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<Results<Ok<List<SavedViewResponse>>, UnauthorizedHttpResult>> GetListAsync(
        [FromServices] ISavedViewStoreReader store,
        string entityType,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId(user);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        IReadOnlyList<SavedView> views = await store
            .GetListAsync(entityType, userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(views.Select(v => MapView(v, userId)).ToList());
    }

    private static async Task<Results<Created<SavedViewResponse>, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(
        CreateSavedViewRequest request,
        [FromServices] ISavedViewStoreReader reader,
        [FromServices] ISavedViewStoreWriter store,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IOptions<QueryEngineOptions> engineOptions,
        string entityType,
        [AsParameters] SavedViewUserContext ctx,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId(ctx.User);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        Guid? tenantId = ctx.Tenant.IsAvailable ? ctx.Tenant.Id : null;
        int existingCount = await reader
            .GetCountAsync(entityType, userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (existingCount >= engineOptions.Value.MaxSavedViewsPerUser)
        {
            return TypedResults.Problem(
                detail: $"Maximum saved views per user reached ({engineOptions.Value.MaxSavedViewsPerUser}).",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        SavedView view = new()
        {
            Id = guidGenerator.Create(),
            EntityType = entityType,
            Name = request.Name,
            UserId = userId,
            IsShared = request.IsShared,
            IsDefault = request.IsDefault,
            FilterJson = request.FilterJson,
            SortJson = request.SortJson,
            GroupByJson = request.GroupByJson,
            VisibleColumnsJson = request.VisibleColumnsJson,
            TenantId = tenantId,
            CreatedAt = ctx.Clock.Now,
            CreatedBy = userId,
        };

        await store.CreateAsync(view, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/saved-views/{view.Id}", MapView(view, userId));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> UpdateAsync(
        Guid id,
        UpdateSavedViewRequest request,
        [FromServices] ISavedViewStoreReader reader,
        [FromServices] ISavedViewStoreWriter writer,
        [FromServices] IClock clock,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId(user);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        SavedView? existing = await reader.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.Problem(detail: "Saved view not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            return TypedResults.Problem(detail: "You do not own this saved view.", statusCode: StatusCodes.Status403Forbidden);
        }

        existing.Name = request.Name;
        existing.IsShared = request.IsShared;
        existing.FilterJson = request.FilterJson;
        existing.SortJson = request.SortJson;
        existing.GroupByJson = request.GroupByJson;
        existing.VisibleColumnsJson = request.VisibleColumnsJson;
        existing.ModifiedAt = clock.Now;
        existing.ModifiedBy = userId;

        await writer.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> DeleteAsync(
        Guid id,
        [FromServices] ISavedViewStoreReader reader,
        [FromServices] ISavedViewStoreWriter store,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId(user);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        SavedView? existing = await reader.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.Problem(detail: "Saved view not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            return TypedResults.Problem(detail: "You do not own this saved view.", statusCode: StatusCodes.Status403Forbidden);
        }

        await store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> SetDefaultAsync(
        Guid id,
        [FromServices] ISavedViewStoreReader reader,
        [FromServices] ISavedViewStoreWriter store,
        string entityType,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId(user);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        SavedView? existing = await reader.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.Problem(detail: "Saved view not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal) && !existing.IsShared)
        {
            return TypedResults.Problem(detail: "You do not own this saved view.", statusCode: StatusCodes.Status403Forbidden);
        }

        await store.SetDefaultAsync(id, userId, entityType, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static SavedViewResponse MapView(SavedView v, string currentUserId) =>
        new(v.Id, v.EntityType, v.Name,
            string.Equals(v.UserId, currentUserId, StringComparison.Ordinal),
            v.IsShared, v.IsDefault,
            v.FilterJson, v.SortJson, v.GroupByJson, v.VisibleColumnsJson);

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value;

    /// <summary>
    /// Groups user identity and cross-cutting services for <see cref="CreateAsync"/>
    /// to stay within the 7-parameter limit.
    /// </summary>
    internal sealed record SavedViewUserContext(
        [FromServices] ICurrentTenant Tenant,
        ClaimsPrincipal User,
        [FromServices] IClock Clock);
}
