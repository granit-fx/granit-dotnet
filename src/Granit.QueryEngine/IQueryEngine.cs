using System.Linq.Expressions;
using Granit.QueryEngine.Meta;
using Granit.QueryEngine.SavedViews;

namespace Granit.QueryEngine;

/// <summary>
/// Query engine that orchestrates filtering, sorting, pagination, and grouping
/// for a given entity type using a <see cref="QueryDefinition{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IQueryEngine<TEntity> where TEntity : class
{
    /// <summary>
    /// Executes a query and returns a paginated result.
    /// </summary>
    /// <param name="source">The base queryable (e.g. from DbContext).</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated result.</returns>
    Task<PagedResult<TEntity>> ExecuteAsync(
        IQueryable<TEntity> source,
        QueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query with a server-side projection and returns a paginated result
    /// containing the projected type. Use this to reduce I/O by selecting only the
    /// columns you need (i.e. <c>SELECT col1, col2</c> instead of <c>SELECT *</c>).
    /// </summary>
    /// <typeparam name="TProjection">The projected result type.</typeparam>
    /// <param name="source">The base queryable (e.g. from DbContext).</param>
    /// <param name="request">The query parameters.</param>
    /// <param name="projection">
    /// A projection expression applied server-side. Must be translatable to SQL by EF Core —
    /// non-translatable expressions (e.g. calling arbitrary C# methods) will throw at runtime.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated result containing projected items.</returns>
    Task<PagedResult<TProjection>> ExecuteAsync<TProjection>(
        IQueryable<TEntity> source,
        QueryRequest request,
        Expression<Func<TEntity, TProjection>> projection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a grouped query and returns a grouped result.
    /// </summary>
    /// <param name="source">The base queryable.</param>
    /// <param name="request">The query parameters (must include <see cref="QueryRequest.GroupBy"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A grouped result.</returns>
    Task<GroupedResult<TEntity>> ExecuteGroupedAsync(
        IQueryable<TEntity> source,
        QueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and streams all matching entities without pagination.
    /// Applies filtering and sorting from the <paramref name="request"/> but returns
    /// every matching row as an async stream — used by the export pipeline.
    /// </summary>
    /// <param name="source">The base queryable (e.g. from DbContext).</param>
    /// <param name="request">The query parameters (pagination fields are ignored).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of all matching entities.</returns>
    IAsyncEnumerable<TEntity> ExecuteStreamAsync(
        IQueryable<TEntity> source,
        QueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates query metadata from the definition for the <c>GET /meta</c> endpoint.
    /// </summary>
    /// <param name="savedViews">Optional saved views to include in the metadata.</param>
    /// <returns>The query metadata.</returns>
    QueryMetadata GetMetadata(IReadOnlyList<SavedViewSummary>? savedViews = null);
}
