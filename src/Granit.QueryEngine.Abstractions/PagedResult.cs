namespace Granit.QueryEngine;

/// <summary>
/// Paginated result for query endpoints.
/// </summary>
/// <typeparam name="T">The item type (entity or DTO).</typeparam>
/// <param name="Items">The matching items for the current page.</param>
/// <param name="TotalCount">
/// The total number of matching items (before pagination).
/// <c>null</c> when the caller opted out via <c>skipTotalCount=true</c>
/// or when using cursor pagination.
/// </param>
/// <param name="HasMore">
/// Whether more pages exist after the current one.
/// When <paramref name="TotalCount"/> is <c>null</c>, this is determined by
/// fetching <c>pageSize + 1</c> rows; otherwise it is derived from the total count.
/// </param>
/// <param name="NextCursor">
/// Opaque cursor for the next page (keyset pagination only).
/// <c>null</c> when there are no more pages or when using offset pagination.
/// </param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int? TotalCount,
    bool HasMore,
    string? NextCursor = null);
