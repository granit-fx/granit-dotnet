namespace Granit.QueryEngine;

/// <summary>
/// Query parameters for filtering, sorting, and paginating a query endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Filters use the <c>filter[field.op]=value</c> query string syntax
/// (e.g. <c>filter[name.contains]=Alice</c>).
/// </para>
/// <para>
/// Sorting uses a comma-separated list with optional <c>-</c> prefix for descending order
/// (e.g. <c>sort=-createdAt,lastName</c>).
/// </para>
/// </remarks>
public sealed record QueryRequest
{
    /// <summary>
    /// One-based page number for offset pagination. Default is <c>1</c>.
    /// Mutually exclusive with <see cref="Cursor"/>.
    /// </summary>
    public int? Page { get; init; }

    /// <summary>
    /// Maximum number of items per page. Clamped to the definition's <c>MaxPageSize</c>.
    /// </summary>
    public int? PageSize { get; init; }

    /// <summary>
    /// Opaque cursor for keyset pagination (base64-encoded).
    /// Mutually exclusive with <see cref="Page"/>.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Free-text search applied to the definition's global search properties.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Comma-separated sort specification (e.g. <c>"-createdAt,lastName"</c>).
    /// Prefix a field with <c>-</c> for descending order.
    /// </summary>
    public string? Sort { get; init; }

    /// <summary>
    /// Active filter criteria parsed from <c>filter[field.op]=value</c> query string pairs.
    /// Keys are <c>"field.operator"</c>, values are the filter values.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Filter { get; init; }

    /// <summary>
    /// Active preset names, keyed by filter group name.
    /// Keys are group names, values are comma-separated preset names.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Presets { get; init; }

    /// <summary>
    /// Active quick filter names. Quick filters are independent toggleable predicates
    /// that combine with AND semantics (e.g. "My Appointments", "Unread").
    /// </summary>
    public IReadOnlyList<string>? QuickFilters { get; init; }

    /// <summary>
    /// Property name to group results by. When set, the query returns
    /// <see cref="GroupedResult{T}"/> instead of <see cref="PagedResult{T}"/>.
    /// </summary>
    public string? GroupBy { get; init; }

    /// <summary>
    /// When <c>true</c>, the server skips the <c>COUNT(*)</c> query and returns
    /// <c>null</c> for <see cref="PagedResult{T}.TotalCount"/>.
    /// <see cref="PagedResult{T}.HasMore"/> is still computed by fetching <c>pageSize + 1</c> rows.
    /// Useful for large datasets where counting is expensive.
    /// </summary>
    public bool SkipTotalCount { get; init; }
}
