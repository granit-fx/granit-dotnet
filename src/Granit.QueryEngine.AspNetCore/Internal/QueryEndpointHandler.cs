using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.Meta;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.QueryEngine.AspNetCore.Internal;

/// <summary>
/// Minimal API endpoint handlers for query and metadata.
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
    internal static Ok<QueryMetadata> GetMetadata<TEntity>(
        [FromServices] IQueryEngine<TEntity> engine)
        where TEntity : class
    {
        QueryMetadata metadata = engine.GetMetadata();
        return TypedResults.Ok(metadata);
    }
}
