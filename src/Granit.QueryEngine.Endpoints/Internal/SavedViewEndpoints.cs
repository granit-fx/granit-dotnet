using System.Security.Claims;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine.Endpoints.Dtos;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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
            [FromServices] ISavedViewStoreWriter store,
            [FromServices] IGuidGenerator guidGenerator,
            [AsParameters] SavedViewUserContext ctx,
            CancellationToken cancellationToken) =>
            CreateAsync(request, store, guidGenerator, entityType, ctx, cancellationToken))
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
            CancellationToken cancellationToken) =>
            UpdateAsync(id, request, reader, writer, clock, cancellationToken))
            .WithName($"UpdateSavedView_{entityType}")
            .WithSummary("Updates an existing saved view.")
            .WithDescription("Replaces the name, filter, sort, and column selection of an existing saved view. Returns 404 if the view does not exist.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        savedViews.MapDelete("/{id:guid}", (
            Guid id,
            [FromServices] ISavedViewStoreWriter store,
            CancellationToken cancellationToken) =>
            DeleteAsync(id, store, cancellationToken))
            .WithName($"DeleteSavedView_{entityType}")
            .WithSummary("Deletes a saved view.")
            .WithDescription("Permanently deletes the saved view. If it was the user's default view, no default is set afterward. Returns 404 if the view does not exist.")
            .Produces(StatusCodes.Status204NoContent);

        savedViews.MapPost("/{id:guid}/set-default", (
            Guid id,
            [FromServices] ISavedViewStoreWriter store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            SetDefaultAsync(id, store, entityType, user, cancellationToken))
            .WithName($"SetDefaultSavedView_{entityType}")
            .WithSummary("Sets a saved view as the default for the current user.")
            .WithDescription("Marks the specified saved view as the user's default for this entity type. The previous default (if any) is unset. The default view is automatically applied when the user opens the list page.")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<Ok<List<SavedViewResponse>>> GetListAsync(
        [FromServices] ISavedViewStoreReader store,
        string entityType,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        IReadOnlyList<SavedView> views = await store
            .GetListAsync(entityType, userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(views.Select(MapView).ToList());
    }

    private static async Task<Created<SavedViewResponse>> CreateAsync(
        CreateSavedViewRequest request,
        [FromServices] ISavedViewStoreWriter store,
        [FromServices] IGuidGenerator guidGenerator,
        string entityType,
        [AsParameters] SavedViewUserContext ctx,
        CancellationToken cancellationToken)
    {
        string userId = GetUserId(ctx.User);

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
            TenantId = ctx.Tenant.IsAvailable ? ctx.Tenant.Id : null,
            CreatedAt = ctx.Clock.Now,
            CreatedBy = userId,
        };

        await store.CreateAsync(view, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/saved-views/{view.Id}", MapView(view));
    }

    private static async Task<Results<NoContent, NotFound>> UpdateAsync(
        Guid id,
        UpdateSavedViewRequest request,
        [FromServices] ISavedViewStoreReader reader,
        [FromServices] ISavedViewStoreWriter writer,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        SavedView? existing = await reader.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        existing.Name = request.Name;
        existing.IsShared = request.IsShared;
        existing.FilterJson = request.FilterJson;
        existing.SortJson = request.SortJson;
        existing.GroupByJson = request.GroupByJson;
        existing.VisibleColumnsJson = request.VisibleColumnsJson;
        existing.ModifiedAt = clock.Now;

        await writer.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteAsync(
        Guid id,
        [FromServices] ISavedViewStoreWriter store,
        CancellationToken cancellationToken)
    {
        await store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetDefaultAsync(
        Guid id,
        [FromServices] ISavedViewStoreWriter store,
        string entityType,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        string userId = GetUserId(user);
        await store.SetDefaultAsync(id, userId, entityType, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static SavedViewResponse MapView(SavedView v) =>
        new(v.Id, v.EntityType, v.Name, v.UserId, v.IsShared, v.IsDefault,
            v.FilterJson, v.SortJson, v.GroupByJson, v.VisibleColumnsJson);

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value
        ?? string.Empty;

    /// <summary>
    /// Groups user identity and cross-cutting services for <see cref="CreateAsync"/>
    /// to stay within the 7-parameter limit.
    /// </summary>
    internal sealed record SavedViewUserContext(
        [FromServices] ICurrentTenant Tenant,
        ClaimsPrincipal User,
        [FromServices] IClock Clock);
}
