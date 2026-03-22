using Granit.Querying;

namespace Granit.ReferenceData;

/// <summary>
/// Query parameters for filtering, sorting, and paginating reference data entries.
/// </summary>
/// <remarks>
/// For advanced querying (operators, shadow properties, cursor pagination),
/// use <see cref="QueryRequest"/> with <c>IQueryEngine&lt;T&gt;</c> instead.
/// </remarks>
/// <param name="ActiveOnly">When <c>true</c> (default), only active entries are returned.</param>
/// <param name="SearchTerm">Optional text to filter by Code or Label (case-insensitive contains).</param>
/// <param name="SortBy">Property name to sort by (e.g., "Code", "Label", "SortOrder"). Default is "SortOrder".</param>
/// <param name="Descending">When <c>true</c>, sort in descending order. Default is <c>false</c>.</param>
/// <param name="Page">One-based page number. Default is 1.</param>
/// <param name="PageSize">Maximum number of entries per page. Default is <see cref="QueryingDefaults.DefaultPageSize"/>.</param>
public sealed record ReferenceDataQuery(
    bool ActiveOnly = true,
    string? SearchTerm = null,
    string? SortBy = "SortOrder",
    bool Descending = false,
    int Page = 1,
    int PageSize = QueryingDefaults.DefaultPageSize);
