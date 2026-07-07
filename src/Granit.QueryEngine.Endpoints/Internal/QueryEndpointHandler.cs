using System.Linq.Expressions;
using Granit.QueryEngine.Meta;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.QueryEngine.Endpoints.Internal;

/// <summary>
/// Minimal API endpoint handlers for query and metadata. The list handlers are invoked
/// through a documented trampoline in <c>MapGranitQuery</c>: they need the setup-time
/// <c>sourceProvider</c> closure, which Minimal API cannot bind, so the route lambda only
/// resolves the source and delegates here.
/// </summary>
internal static class QueryEndpointHandler
{
    /// <summary>
    /// GET / — Executes a paginated or grouped query.
    /// </summary>
    internal static async Task<IResult> QueryAsync<TEntity>(
        [FromServices] IQueryEngine<TEntity> engine,
        QueryRequest request,
        [FromServices] IQueryable<TEntity> source,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (!string.IsNullOrWhiteSpace(request.GroupBy))
        {
            GroupedResult<TEntity> grouped = await engine
                .ExecuteGroupedAsync(source, request, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(grouped);
        }

        PagedResult<TEntity> paged = await engine
            .ExecuteAsync(source, request, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(paged);
    }

    /// <summary>
    /// GET / — Executes a paginated or grouped query projected to <typeparamref name="TDto"/>.
    /// Paged queries project at the SQL level; grouped queries apply the same projection
    /// in-memory after entity materialization so <c>GroupedResult.Items</c> surfaces
    /// <typeparamref name="TDto"/>, never the raw entity.
    /// </summary>
    internal static async Task<IResult> QueryProjectedAsync<TEntity, TDto>(
        [FromServices] IQueryEngine<TEntity> engine,
        QueryRequest request,
        [FromServices] IQueryable<TEntity> source,
        Expression<Func<TEntity, TDto>> projection,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (!string.IsNullOrWhiteSpace(request.GroupBy))
        {
            GroupedResult<TDto> grouped = await engine
                .ExecuteGroupedAsync(source, request, projection, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(grouped);
        }

        PagedResult<TDto> paged = await engine
            .ExecuteAsync(source, request, projection, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(paged);
    }

    /// <summary>
    /// GET /meta — Returns query metadata for frontend auto-configuration.
    /// </summary>
    internal static Ok<QueryMetadata> GetMetadata<TEntity>(
        [FromServices] IQueryEngine<TEntity> engine)
        where TEntity : class
    {
        QueryMetadata metadata = engine.GetMetadata();
        return TypedResults.Ok(metadata);
    }
}
