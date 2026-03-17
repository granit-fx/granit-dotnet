using System.Security.Claims;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Querying.Endpoints.Dtos;
using Granit.Querying.SavedViews;
using Granit.Querying.SavedViews.Domain;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Querying.Endpoints.Internal;

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
            ISavedViewStoreReader store,
            ICurrentTenant tenant,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            GetListAsync(store, entityType, tenant, user, cancellationToken))
            .WithName($"GetSavedViews_{entityType}")
            .WithSummary("Returns saved views for the current user.")
            .WithDescription("Returns all saved views owned by the authenticated user for this entity type within the current tenant. Each view contains its filter, sort, column selection, and whether it is the user's default view.");

        savedViews.MapPost("/", (
            CreateSavedViewRequest request,
            ISavedViewStoreWriter store,
            [FromServices] IGuidGenerator guidGenerator,
            ICurrentTenant tenant,
            ClaimsPrincipal user,
            [FromServices] IClock clock,
            CancellationToken cancellationToken) =>
            CreateAsync(request, store, guidGenerator, entityType, tenant, user, clock, cancellationToken))
            .WithName($"CreateSavedView_{entityType}")
            .WithSummary("Creates a new saved view.")
            .WithDescription("Creates a new saved view for the current user and entity type. The view stores a reusable query configuration (filters, sort, column selection). Returns 201 Created with the saved view details.");

        savedViews.MapPut("/{id:guid}", (
            Guid id,
            UpdateSavedViewRequest request,
            ISavedViewStoreReader reader,
            ISavedViewStoreWriter writer,
            [FromServices] IClock clock,
            CancellationToken cancellationToken) =>
            UpdateAsync(id, request, reader, writer, clock, cancellationToken))
            .WithName($"UpdateSavedView_{entityType}")
            .WithSummary("Updates an existing saved view.")
            .WithDescription("Replaces the name, filter, sort, and column selection of an existing saved view. Returns 404 if the view does not exist.");

        savedViews.MapDelete("/{id:guid}", (
            Guid id,
            ISavedViewStoreWriter store,
            CancellationToken cancellationToken) =>
            DeleteAsync(id, store, cancellationToken))
            .WithName($"DeleteSavedView_{entityType}")
            .WithSummary("Deletes a saved view.")
            .WithDescription("Permanently deletes the saved view. If it was the user's default view, no default is set afterward. Returns 404 if the view does not exist.");

        savedViews.MapPost("/{id:guid}/set-default", (
            Guid id,
            ISavedViewStoreWriter store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            SetDefaultAsync(id, store, entityType, user, cancellationToken))
            .WithName($"SetDefaultSavedView_{entityType}")
            .WithSummary("Sets a saved view as the default for the current user.")
            .WithDescription("Marks the specified saved view as the user's default for this entity type. The previous default (if any) is unset. The default view is automatically applied when the user opens the list page.");
    }

    private static async Task<Ok<List<SavedViewResponse>>> GetListAsync(
        ISavedViewStoreReader store,
        string entityType,
        ICurrentTenant tenant,
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
        ISavedViewStoreWriter store,
        IGuidGenerator guidGenerator,
        string entityType,
        ICurrentTenant tenant,
        ClaimsPrincipal user,
        IClock clock,
        CancellationToken cancellationToken)
    {
        string userId = GetUserId(user);

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
            TenantId = tenant.IsAvailable ? tenant.Id : null,
            CreatedAt = clock.Now,
            CreatedBy = userId,
        };

        await store.CreateAsync(view, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/saved-views/{view.Id}", MapView(view));
    }

    private static async Task<Results<NoContent, NotFound>> UpdateAsync(
        Guid id,
        UpdateSavedViewRequest request,
        ISavedViewStoreReader reader,
        ISavedViewStoreWriter writer,
        IClock clock,
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
        ISavedViewStoreWriter store,
        CancellationToken cancellationToken)
    {
        await store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetDefaultAsync(
        Guid id,
        ISavedViewStoreWriter store,
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
}
