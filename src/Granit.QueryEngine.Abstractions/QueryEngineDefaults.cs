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
    /// Maximum number of aggregates per query definition (each aggregate occupies a fixed
    /// slot in the grouped SQL projection).
    /// </summary>
    public const int MaxAggregates = 8;

    /// <summary>
    /// Clamps <paramref name="page"/> and <paramref name="pageSize"/> to valid pagination ranges
    /// using the compile-time default cap (<see cref="MaxPageSize"/> = 100). This DI-free guard
    /// intentionally ignores a host-configured <c>QueryEngineOptions.MaxPageSize</c> — call sites
    /// that have options in scope should prefer
    /// <see cref="ClampPagination(int, int, Granit.QueryEngine.Options.QueryEngineOptions)"/> so
    /// the configured cap is the single source of truth.
    /// </summary>
    /// <param name="page">One-based page number (clamped to minimum 1).</param>
    /// <param name="pageSize">Items per page (clamped between 1 and <see cref="MaxPageSize"/>).</param>
    /// <returns>A tuple of clamped (Page, PageSize) values.</returns>
    public static (int Page, int PageSize) ClampPagination(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));

    /// <summary>
    /// Clamps <paramref name="page"/> and <paramref name="pageSize"/> against the
    /// host-configured <see cref="Granit.QueryEngine.Options.QueryEngineOptions.MaxPageSize"/>.
    /// </summary>
    /// <param name="page">One-based page number (clamped to minimum 1).</param>
    /// <param name="pageSize">Items per page (clamped between 1 and the configured maximum).</param>
    /// <param name="options">The effective query engine options.</param>
    /// <returns>A tuple of clamped (Page, PageSize) values.</returns>
    public static (int Page, int PageSize) ClampPagination(
        int page, int pageSize, Granit.QueryEngine.Options.QueryEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return (Math.Max(page, 1), Math.Clamp(pageSize, 1, options.MaxPageSize));
    }
}
