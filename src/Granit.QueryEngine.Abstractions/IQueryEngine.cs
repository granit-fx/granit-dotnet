using System.Linq.Expressions;
using Granit.QueryEngine.Meta;

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
    /// Executes a grouped query and projects each materialized item to <typeparamref name="TProjection"/>
    /// before assembling the <see cref="GroupedResult{T}"/>. The grouping is computed on the entity
    /// columns (the <see cref="QueryRequest.GroupBy"/> property must exist on <typeparamref name="TEntity"/>),
    /// then items inside each group are projected so the response contract exposes the projected shape
    /// instead of the raw entity.
    /// </summary>
    /// <typeparam name="TProjection">The projected item type returned in <c>GroupedResult&lt;TProjection&gt;.Groups[].Items</c>.</typeparam>
    /// <param name="source">The base queryable.</param>
    /// <param name="request">The query parameters (must include <see cref="QueryRequest.GroupBy"/>).</param>
    /// <param name="projection">
    /// A projection expression applied to each materialized entity. Compiled and applied in-memory
    /// after materialization (the group-by property must remain on the entity for key extraction).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A grouped result whose items are projected.</returns>
    Task<GroupedResult<TProjection>> ExecuteGroupedAsync<TProjection>(
        IQueryable<TEntity> source,
        QueryRequest request,
        Expression<Func<TEntity, TProjection>> projection,
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
    /// <returns>The query metadata.</returns>
    QueryMetadata GetMetadata();

    /// <summary>
    /// Applies the <see cref="QueryDefinition{TEntity}"/>'s filter pipeline (filters, presets,
    /// quick filters, global search) to <paramref name="source"/> and returns the unsorted,
    /// unpaginated, unprojected <see cref="IQueryable{T}"/>.
    /// </summary>
    /// <param name="source">The base queryable.</param>
    /// <param name="request">The query parameters (only filter-related fields are applied).</param>
    /// <returns>The filtered queryable, ready for downstream aggregation.</returns>
    /// <remarks>
    /// <para>
    /// Use this entry point from analytics modules (<c>Granit.Analytics</c> metric executors,
    /// dashboard widget renderers) to apply aggregations such as <c>Count</c>, <c>Sum</c>,
    /// <c>Avg</c>, <c>Min</c>, <c>Max</c> against the same set of rows the grid endpoint sees —
    /// so that a KPI like "12 unpaid invoices" exactly matches the grid's row count for the
    /// same filter spec.
    /// </para>
    /// <para>
    /// Sort, projection, pagination, and group-by are <b>NOT</b> applied. Multi-tenancy and
    /// soft-delete filters are expected to be applied by the caller on <paramref name="source"/>
    /// (typically via <c>ApplyGranitConventions</c> on the DbContext) before invoking this method.
    /// </para>
    /// </remarks>
    IQueryable<TEntity> BuildFilteredQuery(IQueryable<TEntity> source, QueryRequest request);
}
