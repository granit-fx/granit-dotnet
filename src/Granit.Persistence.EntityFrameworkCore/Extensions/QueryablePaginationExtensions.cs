using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for <see cref="IQueryable{T}"/> pagination.
/// </summary>
public static class QueryablePaginationExtensions
{
    /// <summary>
    /// Executes a paginated query, returning a <see cref="PagedResult{T}"/> with
    /// <c>TotalCount</c> and <c>HasMore</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Issues two queries: one for the total count and one for the page items.
    /// For large datasets where count is expensive, prefer cursor pagination via
    /// <see cref="Granit.QueryEngine"/> endpoints.
    /// </para>
    /// <para>
    /// <b>Important:</b> The caller MUST apply an <c>OrderBy</c> clause to
    /// <paramref name="source"/> before calling this method. Without a deterministic
    /// ordering, <c>Skip</c>/<c>Take</c> produces unpredictable results and EF Core
    /// emits a runtime warning.
    /// </para>
    /// </remarks>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int totalCount = await source.CountAsync(cancellationToken).ConfigureAwait(false);

        List<T> items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<T>(
            items,
            totalCount,
            HasMore: ((page - 1) * pageSize) + items.Count < totalCount);
    }
}
