namespace Granit.QueryEngine;

/// <summary>
/// Default values for query definitions.
/// </summary>
public static class QueryEngineDefaults
{
    /// <summary>Default page size for paginated queries.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maximum allowed page size.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Maximum number of items returned by <c>ExecuteStreamAsync</c>.</summary>
    public const int MaxStreamSize = 100_000;

    /// <summary>
    /// Clamps <paramref name="page"/> and <paramref name="pageSize"/> to valid pagination ranges.
    /// </summary>
    /// <param name="page">One-based page number (clamped to minimum 1).</param>
    /// <param name="pageSize">Items per page (clamped between 1 and <see cref="MaxPageSize"/>).</param>
    /// <returns>A tuple of clamped (Page, PageSize) values.</returns>
    public static (int Page, int PageSize) ClampPagination(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));
}
