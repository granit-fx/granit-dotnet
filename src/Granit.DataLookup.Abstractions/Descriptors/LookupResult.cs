namespace Granit.DataLookup.Descriptors;

/// <summary>
/// Canonical paginated response for a lookup search.
/// </summary>
/// <param name="Items">The page of items, in source order.</param>
/// <param name="TotalCount">
/// Total count across all pages when known. May be <see langword="null"/> for large or
/// cursor-based sources where computing a count is expensive.
/// </param>
/// <param name="ContinuationToken">
/// Opaque token to fetch the next page. When <see langword="null"/>, offset pagination
/// via <c>page</c> / <c>pageSize</c> is used and the caller paginates with
/// <see cref="TotalCount"/>.
/// </param>
public sealed record LookupResult(
    IReadOnlyList<LookupItem> Items,
    int? TotalCount = null,
    string? ContinuationToken = null);
