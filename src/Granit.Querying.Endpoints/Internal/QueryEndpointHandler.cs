using Granit.Querying.Endpoints.Dtos;
using Granit.Querying.Meta;
using Granit.Querying.SavedViews;
using Granit.Querying.SavedViews.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.Querying.Endpoints.Internal;

/// <summary>
/// Minimal API endpoint handlers for query, metadata, and saved views.
/// </summary>
internal static class QueryEndpointHandler
{
    /// <summary>
    /// GET / — Executes a paginated or grouped query.
    /// </summary>
    internal static async Task<IResult> QueryAsync<TEntity>(
        [FromServices] IQueryEngine<TEntity> engine,
        BindableQueryRequest request,
        [FromServices] IQueryable<TEntity> source,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        QueryRequest queryRequest = request.Value;

        if (!string.IsNullOrWhiteSpace(queryRequest.GroupBy))
        {
            GroupedResult<TEntity> grouped = await engine
                .ExecuteGroupedAsync(source, queryRequest, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(grouped);
        }

        PagedResult<TEntity> paged = await engine
            .ExecuteAsync(source, queryRequest, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(paged);
    }

    /// <summary>
    /// GET /meta — Returns query metadata for frontend auto-configuration.
    /// </summary>
    internal static async Task<Ok<QueryMetadata>> GetMetadataAsync<TEntity>(
        [FromServices] IQueryEngine<TEntity> engine,
        [FromServices] ISavedViewStoreReader savedViewStore,
        QueryDefinition<TEntity> definition,
        [FromServices] Granit.Core.MultiTenancy.ICurrentTenant tenant,
        System.Security.Claims.ClaimsPrincipal user,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        string userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? string.Empty;

        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        IReadOnlyList<SavedViewSummary> savedViews;
        try
        {
            IReadOnlyList<SavedView> views = await savedViewStore
                .GetListAsync(definition.Name, userId, tenantId, cancellationToken)
                .ConfigureAwait(false);

            savedViews = views.Select(v => new SavedViewSummary(
                v.Id, v.Name, v.IsShared, v.IsDefault)).ToList();
        }
        catch (NotImplementedException)
        {
            // NullSavedViewStore — no persistence configured
            savedViews = [];
        }

        QueryMetadata metadata = engine.GetMetadata(savedViews);
        return TypedResults.Ok(metadata);
    }
}
