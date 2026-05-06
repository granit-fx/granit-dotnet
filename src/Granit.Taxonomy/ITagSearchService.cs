namespace Granit.Taxonomy;

/// <summary>
/// Cross-entity tag search — answers "show me everything tagged X" with results
/// grouped by <c>TargetType</c> per ADR-054.
/// </summary>
/// <remarks>
/// The search returns the <c>TargetId</c> of every assignment matching the query.
/// Hosts that render the result MUST combine these ids with their own per-entity
/// ACL: a tag-assignment row leaks the existence of an entity even when the
/// caller has no per-entity read permission.
/// </remarks>
public interface ITagSearchService
{
    /// <summary>
    /// Returns tags whose <c>Name</c> matches <paramref name="query"/> (case-insensitive
    /// prefix) along with the assignments grouped by <c>TargetType</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="query"/> is matched as a case-insensitive prefix against
    /// <c>Tag.Name</c>. Pass <paramref name="scope"/> = <c>"*"</c> for cross-scope.
    /// Pagination via <paramref name="skip"/> (default 0) and <paramref name="take"/>
    /// (default 50, max 500).
    /// </remarks>
    Task<TagSearchResult> SearchAsync(
        string query,
        string scope,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>Aggregate result of a cross-entity tag search.</summary>
/// <param name="Tags">Tags whose name matched the query (paged).</param>
/// <param name="HitsByTargetType">
/// Assignments grouped by <c>TargetType</c>. Each hit carries the matching
/// <c>TargetId</c> and the ids of the matching tags applied to it.
/// </param>
/// <param name="TotalCount">Total number of matching tags before pagination.</param>
/// <param name="Skip">Pagination offset applied.</param>
/// <param name="Take">Pagination page size applied.</param>
public sealed record TagSearchResult(
    IReadOnlyList<TagSearchTag> Tags,
    IReadOnlyDictionary<string, IReadOnlyList<TagSearchHit>> HitsByTargetType,
    int TotalCount,
    int Skip,
    int Take);

/// <summary>A tag returned by <see cref="ITagSearchService.SearchAsync"/>.</summary>
public sealed record TagSearchTag(Guid Id, string Name, string Color, string Scope);

/// <summary>One target hit — the entity that carries one or more matching tags.</summary>
public sealed record TagSearchHit(Guid TargetId, IReadOnlyList<Guid> TagIds);
