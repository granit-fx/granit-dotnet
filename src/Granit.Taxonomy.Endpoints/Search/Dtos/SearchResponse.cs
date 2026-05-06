namespace Granit.Taxonomy.Endpoints.Search.Dtos;

/// <summary>Wire-shape response for <c>GET /api/v1/taxonomy/search</c>.</summary>
/// <param name="Tags">Tags whose name matched the query (paged).</param>
/// <param name="Hits">
/// Assignments grouped by <c>TargetType</c>. Each entry is a list of target hits
/// — the <c>TargetId</c> plus the ids of the matching tags applied to it.
/// </param>
/// <param name="TotalCount">Total number of matching tags before pagination.</param>
/// <param name="Skip">Pagination offset applied.</param>
/// <param name="Take">Pagination page size applied.</param>
public sealed record SearchResponse(
    IReadOnlyList<SearchTagItem> Tags,
    IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> Hits,
    int TotalCount,
    int Skip,
    int Take);

/// <summary>A tag in the <see cref="SearchResponse.Tags"/> list.</summary>
public sealed record SearchTagItem(Guid Id, string Name, string Color, string Scope);

/// <summary>One target hit grouped under <see cref="SearchResponse.Hits"/>.</summary>
public sealed record SearchHit(Guid TargetId, IReadOnlyList<Guid> TagIds);
